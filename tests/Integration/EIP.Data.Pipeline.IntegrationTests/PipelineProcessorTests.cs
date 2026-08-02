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
/// Prova, contra um SQL Server real (Testcontainers), as duas garantias centrais do Pipeline (E3.3/
/// E3.4, docs/04-Modelo-Canonico.md §7/§8): reprocessar o mesmo lote nunca duplica um registro
/// canônico (idempotência pela chave de negócio), e uma referência não resolvida vira quarentena —
/// nunca uma exceção que aborta o lote inteiro.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class PipelineProcessorTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPipelineProcessor _pipelineProcessor;
    private readonly IDbContextFactory<CanonicalDbContext> _dbContextFactory;

    public PipelineProcessorTests(SqlServerContainerFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
        services.AddSingleton<TenantSessionContextInterceptor>();
        services.AddDbContextFactory<CanonicalDbContext>((sp, options) =>
            options.UseSqlServer(fixture.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));
        services.AddSingleton<ICanonicalRecordStore, CanonicalRecordStore>();
        services.AddSingleton<IPipelineProcessor, PipelineProcessor>();

        _services = services.BuildServiceProvider();
        _tenantContextAccessor = _services.GetRequiredService<ITenantContextAccessor>();
        _pipelineProcessor = _services.GetRequiredService<IPipelineProcessor>();
        _dbContextFactory = _services.GetRequiredService<IDbContextFactory<CanonicalDbContext>>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task ProcessAsync_ReprocessingTheSameCustomer_NeverDuplicatesTheCanonicalRecord()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var sourceSystemId = Guid.NewGuid();
        var rawContent = Encoding.UTF8.GetBytes(
            """[{"code":"C001","name":"Cliente Um","email":"c1@test.com"}]""");

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            // Mesmo SourceSystemId nas duas chamadas: é isso que simula o mesmo conector
            // reprocessando o mesmo lote de origem — a chave de negócio (docs/04 §4.1) depende disso.
            var first = BuildRequest(tenantId, companyId, sourceSystemId, CanonicalSourceEntities.Customers, rawContent);
            var second = BuildRequest(tenantId, companyId, sourceSystemId, CanonicalSourceEntities.Customers, rawContent);

            var firstResult = await _pipelineProcessor.ProcessAsync(first, CancellationToken.None);
            var secondResult = await _pipelineProcessor.ProcessAsync(second, CancellationToken.None);

            firstResult.AcceptedCount.Should().Be(1);
            secondResult.AcceptedCount.Should().Be(1);

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var matches = await db.Customers.Where(c => c.TenantId == tenantId && c.Code == "C001").ToListAsync();
            matches.Should().HaveCount(1);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ProcessAsync_SalesInvoiceWithUnresolvableCustomerCode_IsQuarantined_WithoutThrowing()
    {
        var tenantId = Guid.NewGuid();
        var rawContent = Encoding.UTF8.GetBytes(
            """
            [{"invoiceNumber":"NF-X","issueDate":"2026-01-01","customerCode":"DOES-NOT-EXIST","currencyCode":"BRL",
              "items":[{"lineNumber":1,"productCode":"DOES-NOT-EXIST","quantity":1,"unitPrice":10}]}]
            """);

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var request = BuildRequest(tenantId, Guid.NewGuid(), Guid.NewGuid(), CanonicalSourceEntities.SalesInvoices, rawContent);

            var result = await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);

            result.ExtractedCount.Should().Be(1);
            result.AcceptedCount.Should().Be(0);
            result.RejectedCount.Should().Be(1);

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var quarantineEntries = await db.QuarantineEntries.Where(q => q.TenantId == tenantId).ToListAsync();
            quarantineEntries.Should().ContainSingle(q => q.FailedRule == "unresolved-reference");

            var invoices = await db.SalesInvoices.Where(i => i.TenantId == tenantId).ToListAsync();
            invoices.Should().BeEmpty();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
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
