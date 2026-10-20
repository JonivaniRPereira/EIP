using System.Text;
using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Infrastructure;
using EIP.Shared.Contracts.Canonical;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Data.Pipeline.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (Testcontainers), a reconciliação Canônico↔Origem (E4.3,
/// docs/04-Modelo-Canonico.md §8.3): uma divergência acima da tolerância configurada precisa ser
/// detectável — o bloqueio automático de publicação fica para E5/E6, aqui só a verificação em si.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CanonicalReconciliationServiceTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPipelineProcessor _pipelineProcessor;
    private readonly ICanonicalReconciliationService _reconciliationService;

    public CanonicalReconciliationServiceTests(SqlServerContainerFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
        services.AddSingleton<TenantSessionContextInterceptor>();
        services.AddDbContextFactory<CanonicalDbContext>((sp, options) =>
            options.UseSqlServer(fixture.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));
        services.AddSingleton<ICanonicalRecordStore, CanonicalRecordStore>();
        services.AddSingleton<IPipelineProcessor, PipelineProcessor>();
        services.AddSingleton<ICanonicalReconciliationService, CanonicalReconciliationService>();

        _services = services.BuildServiceProvider();
        _tenantContextAccessor = _services.GetRequiredService<ITenantContextAccessor>();
        _pipelineProcessor = _services.GetRequiredService<IPipelineProcessor>();
        _reconciliationService = _services.GetRequiredService<ICanonicalReconciliationService>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task ReconcileSalesInvoicesAsync_WhenReportedMatchesPersisted_IsWithinTolerance()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var invoiceSourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAndProductAsync(tenantId, companyId);

            // quantity=2 * unitPrice=50 - discount=0 => NetAmount = 100.
            var invoiceContent = Encoding.UTF8.GetBytes(
                """
                [{"invoiceNumber":"NF-REC-1","issueDate":"2026-01-01","customerCode":"REC-C1","currencyCode":"BRL",
                  "items":[{"lineNumber":1,"productCode":"REC-P1","quantity":2,"unitPrice":50}]}]
                """);
            var invoiceRequest = BuildRequest(tenantId, companyId, invoiceSourceSystemId, CanonicalSourceEntities.SalesInvoices, invoiceContent);
            var invoiceResult = await _pipelineProcessor.ProcessAsync(invoiceRequest, CancellationToken.None);
            invoiceResult.AcceptedCount.Should().Be(1);

            var reconciliation = await _reconciliationService.ReconcileSalesInvoicesAsync(
                tenantId, invoiceSourceSystemId, reportedCount: 1, reportedNetAmountTotal: 100m, toleranceFraction: 0.01m, CancellationToken.None);

            reconciliation.IsWithinTolerance.Should().BeTrue();
            reconciliation.ActualCount.Should().Be(1);
            reconciliation.ActualNetAmountTotal.Should().Be(100m);
            reconciliation.Discrepancy.Should().BeNull();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ReconcileSalesInvoicesAsync_WhenReportedAmountDivergesBeyondTolerance_IsDetected()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var invoiceSourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAndProductAsync(tenantId, companyId);

            var invoiceContent = Encoding.UTF8.GetBytes(
                """
                [{"invoiceNumber":"NF-REC-2","issueDate":"2026-01-01","customerCode":"REC-C1","currencyCode":"BRL",
                  "items":[{"lineNumber":1,"productCode":"REC-P1","quantity":2,"unitPrice":50}]}]
                """);
            var invoiceRequest = BuildRequest(tenantId, companyId, invoiceSourceSystemId, CanonicalSourceEntities.SalesInvoices, invoiceContent);
            await _pipelineProcessor.ProcessAsync(invoiceRequest, CancellationToken.None);

            // Persistido é 100, mas o "relatório" alega 500 — divergência bem acima de 1% de tolerância.
            var reconciliation = await _reconciliationService.ReconcileSalesInvoicesAsync(
                tenantId, invoiceSourceSystemId, reportedCount: 1, reportedNetAmountTotal: 500m, toleranceFraction: 0.01m, CancellationToken.None);

            reconciliation.IsWithinTolerance.Should().BeFalse();
            reconciliation.ActualNetAmountTotal.Should().Be(100m);
            reconciliation.Discrepancy.Should().NotBeNullOrEmpty();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Theory]
    [InlineData(false, 1, 100, true)]
    [InlineData(true, 1, 150, true)]
    [InlineData(false, 1, 500, false)]
    [InlineData(true, 1, 500, false)]
    [InlineData(false, 2, 100, false)]
    [InlineData(false, 1, 101, true)]
    [InlineData(false, 1, 102, false)]
    public async Task ReconcileIncrementalBatch_WithHistory_UsesOnlyPersistedBatchAndDetectsDivergence(
        bool updateExisting, int reportedCount, int reportedAmount, bool expectedMatch)
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAndProductAsync(tenantId, companyId);
            await PersistInvoiceAsync(tenantId, companyId, sourceId, "OLD-1", 100);
            await PersistInvoiceAsync(tenantId, companyId, sourceId, "OLD-2", 100);
            var batch = await PersistInvoiceAsync(tenantId, companyId, sourceId,
                updateExisting ? "OLD-1" : "NEW-1", updateExisting ? 150 : 100);

            // Mesmo raw em outra origem não pode entrar nos totais do conector alvo.
            await PersistInvoiceAsync(tenantId, companyId, Guid.NewGuid(), "FOREIGN", 1000, batch.RawObjectUri);
            var result = await _reconciliationService.ReconcileSalesInvoicesAsync(
                tenantId, sourceId, reportedCount, reportedAmount, 0.01m, CancellationToken.None, batch.RawObjectUri);

            result.IsWithinTolerance.Should().Be(expectedMatch);
            result.ActualCount.Should().Be(1);
            result.ActualNetAmountTotal.Should().Be(updateExisting ? 150m : 100m);
            if (expectedMatch)
            {
                result.Discrepancy.Should().BeNull();
            }
            else
            {
                result.Discrepancy.Should().NotBeNullOrEmpty();
            }

            // Sem filtro, o contrato histórico continua consultando toda a origem.
            var totals = await _reconciliationService.ReconcileSalesInvoicesAsync(
                tenantId, sourceId, updateExisting ? 2 : 3, updateExisting ? 250m : 300m,
                0.01m, CancellationToken.None);
            totals.IsWithinTolerance.Should().BeTrue();
            var incompleteFullReport = await _reconciliationService.ReconcileSalesInvoicesAsync(
                tenantId, sourceId, 1, updateExisting ? 150m : 100m, 0.01m, CancellationToken.None);
            incompleteFullReport.IsWithinTolerance.Should().BeFalse(
                "um relatório completo não pode omitir o histórico persistido");
        }
        finally { _tenantContextAccessor.Current = null; }
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 100, false)]
    public async Task ReconcileUnpersistedBatch_WithHistory_DoesNotCountHistoricalRows(
        int reportedCount, int reportedAmount, bool expectedMatch)
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAndProductAsync(tenantId, companyId);
            await PersistInvoiceAsync(tenantId, companyId, sourceId, "HISTORY", 100);
            var result = await _reconciliationService.ReconcileSalesInvoicesAsync(
                tenantId, sourceId, reportedCount, reportedAmount, 0.01m,
                CancellationToken.None, $"tests/{Guid.NewGuid():N}.json");
            result.IsWithinTolerance.Should().Be(expectedMatch);
            result.ActualCount.Should().Be(0);
            result.ActualNetAmountTotal.Should().Be(0);
        }
        finally { _tenantContextAccessor.Current = null; }
    }

    private async Task<PipelineProcessingRequest> PersistInvoiceAsync(
        Guid tenantId, Guid companyId, Guid sourceId, string number, int amount, string? rawKey = null)
    {
        var content = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] {
            new { invoiceNumber = number, issueDate = "2026-01-01", customerCode = "REC-C1", currencyCode = "BRL",
                items = new[] { new { lineNumber = 1, productCode = "REC-P1", quantity = 1, unitPrice = amount } } }
        });
        var request = BuildRequest(tenantId, companyId, sourceId, CanonicalSourceEntities.SalesInvoices, content);
        if (rawKey is not null)
        {
            request = request with { RawObjectUri = rawKey };
        }
        var result = await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
        result.AcceptedCount.Should().Be(1);
        return request;
    }

    private async Task SeedCustomerAndProductAsync(Guid tenantId, Guid companyId)
    {
        var customerContent = Encoding.UTF8.GetBytes("""[{"code":"REC-C1","name":"Cliente Reconciliação"}]""");
        var customerRequest = BuildRequest(tenantId, companyId, Guid.NewGuid(), CanonicalSourceEntities.Customers, customerContent);
        await _pipelineProcessor.ProcessAsync(customerRequest, CancellationToken.None);

        var productContent = Encoding.UTF8.GetBytes("""[{"code":"REC-P1","name":"Produto Reconciliação","productType":"Product"}]""");
        var productRequest = BuildRequest(tenantId, companyId, Guid.NewGuid(), CanonicalSourceEntities.Products, productContent);
        await _pipelineProcessor.ProcessAsync(productRequest, CancellationToken.None);
    }

    private static PipelineProcessingRequest BuildRequest(Guid tenantId, Guid companyId, Guid sourceSystemId, string sourceEntity, byte[] rawContent) =>
        new(
            tenantId,
            companyId,
            sourceSystemId,
            SyncRunId: Guid.NewGuid(),
            sourceEntity,
            CorrelationId: Guid.NewGuid().ToString(),
            RawObjectUri: $"tests/{Guid.NewGuid():N}.json",
            RawContent: rawContent);
}
