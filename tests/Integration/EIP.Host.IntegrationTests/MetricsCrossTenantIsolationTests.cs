using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Warehouse.Domain;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o critério de saída do E6.2 (docs/roadmap/fase-1-backlog.md): tenant A nunca vê números de
/// tenant B na camada semântica, mesmo os dois tendo dados reais em <c>FactSalesInvoiceItem</c>.
/// Semeia os fatos diretamente via EF Core (o caminho completo Host→Gateway→Worker→Canônico→
/// Warehouse já é validado ao vivo em outros pontos desta fase) — aqui o alvo é só a fronteira de
/// isolamento do endpoint de métricas em si.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class MetricsCrossTenantIsolationTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _emailA = $"metrics-a-{Guid.NewGuid():N}@test.com";
    private readonly string _emailB = $"metrics-b-{Guid.NewGuid():N}@test.com";

    private Guid _tenantAId;
    private Guid _tenantBId;
    private string _tokenA = null!;
    private string _tokenB = null!;

    public MetricsCrossTenantIsolationTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var userAId = await RegisterAsync(_emailA);
        var userBId = await RegisterAsync(_emailB);

        (_tenantAId, _tenantBId) = await ProvisionTenantsAndMembershipsAsync(userAId, userBId);

        _tokenA = await LoginAsync(_emailA);
        _tokenB = await LoginAsync(_emailB);

        await SeedFactSalesInvoiceItemAsync(_tenantAId, netAmount: 1000m);
        await SeedFactSalesInvoiceItemAsync(_tenantBId, netAmount: 99999m);
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        await using (var warehouseDb = await warehouseDbFactory.CreateDbContextAsync())
        {
            await warehouseDb.FactSalesInvoiceItems.Where(f => f.TenantId == _tenantAId || f.TenantId == _tenantBId).ExecuteDeleteAsync();
        }

        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
        await using (var tenantDb = await tenantDbFactory.CreateDbContextAsync())
        {
            await tenantDb.Memberships.Where(m => m.TenantId == _tenantAId || m.TenantId == _tenantBId).ExecuteDeleteAsync();
            await tenantDb.Tenants.Where(t => t.Id == _tenantAId || t.Id == _tenantBId).ExecuteDeleteAsync();
        }

        tenantContextAccessor.Current = null;

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<EIP.Platform.Identity.Infrastructure.AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _emailA || u.Email == _emailB).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task GetCommercial_ReturnsOnlyTheAuthenticatedTenantsOwnNetRevenue()
    {
        using var requestA = CreateRequest("/api/v1/metrics/commercial", _tokenA);
        using var responseA = await _client.SendAsync(requestA);
        responseA.EnsureSuccessStatusCode();
        var bodyA = await responseA.Content.ReadFromJsonAsync<JsonElement>();
        bodyA.GetProperty("netRevenue").GetProperty("value").GetDecimal().Should().Be(1000m);

        using var requestB = CreateRequest("/api/v1/metrics/commercial", _tokenB);
        using var responseB = await _client.SendAsync(requestB);
        responseB.EnsureSuccessStatusCode();
        var bodyB = await responseB.Content.ReadFromJsonAsync<JsonElement>();
        bodyB.GetProperty("netRevenue").GetProperty("value").GetDecimal().Should().Be(99999m);
    }

    private async Task<Guid> RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName = email });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(DecodeSubClaim(body.GetProperty("accessToken").GetString()!));
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    /// <summary>Owner em ambos os tenants, para ter `metrics.view`.</summary>
    private async Task<(Guid TenantAId, Guid TenantBId)> ProvisionTenantsAndMembershipsAsync(Guid userAId, Guid userBId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenantA = Tenant.Create("Metrics Tenant A (E6)", $"e6-a-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
        var tenantB = Tenant.Create("Metrics Tenant B (E6)", $"e6-b-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

        tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await tenantDbFactory.CreateDbContextAsync();
            var membershipA = Membership.Create(userAId, tenantA.Id, MembershipRole.Owner);
            var membershipB = Membership.Create(userBId, tenantB.Id, MembershipRole.Owner);
            membershipA.Activate();
            membershipB.Activate();

            db.Tenants.AddRange(tenantA, tenantB);
            db.Memberships.AddRange(membershipA, membershipB);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }

        return (tenantA.Id, tenantB.Id);
    }

    private async Task SeedFactSalesInvoiceItemAsync(Guid tenantId, decimal netAmount)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();

        var fact = FactSalesInvoiceItem.Create(
            tenantId,
            tenantKey: 1,
            companyKey: 1,
            dateKey: 20260101,
            customerKey: 1,
            productKey: 1,
            currencyKey: 1,
            sourceSystemId: Guid.NewGuid(),
            sourceEntity: "sales-invoices",
            sourceRecordId: $"NF-ISOLATION-{Guid.NewGuid():N}-1",
            salesInvoiceId: Guid.NewGuid(),
            salesInvoiceItemId: Guid.NewGuid(),
            rawObjectUri: $"tests/{Guid.NewGuid():N}.json",
            loadBatchId: Guid.NewGuid(),
            invoiceNumber: "NF-ISOLATION",
            status: "Issued",
            lineNumber: 1,
            quantity: 1m,
            grossAmount: netAmount,
            discountAmount: 0m,
            taxAmount: null,
            netAmount: netAmount);

        tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = await warehouseDbFactory.CreateDbContextAsync();
            db.FactSalesInvoiceItems.Add(fact);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }
    }

    private static HttpRequestMessage CreateRequest(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static string DecodeSubClaim(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        using var document = JsonDocument.Parse(Base64UrlDecode(payloadSegment));
        return document.RootElement.GetProperty("sub").GetString()!;
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };

        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
