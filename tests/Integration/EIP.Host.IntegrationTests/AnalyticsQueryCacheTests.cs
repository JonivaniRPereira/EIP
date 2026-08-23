using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// Prova o critério de aceite do E1.4 (`docs/roadmap/fase-2-backlog.md`): uma segunda consulta
/// idêntica é servida do cache (nunca gera nova query no Warehouse) e o cache nunca vaza entre
/// tenants — mesma consulta, tenants diferentes, entradas de cache diferentes (contadas via
/// <see cref="AnalyticsQueryCallCounter"/>, nunca inspecionando o Redis diretamente).
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class AnalyticsQueryCacheTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;
    private readonly AnalyticsQueryCallCounter _callCounter;

    private const string Password = "SenhaForte!123";
    private readonly string _emailA = $"analytics-cache-a-{Guid.NewGuid():N}@test.com";
    private readonly string _emailB = $"analytics-cache-b-{Guid.NewGuid():N}@test.com";

    private Guid _tenantAId;
    private Guid _tenantBId;
    private string _tokenA = null!;
    private string _tokenB = null!;

    public AnalyticsQueryCacheTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
        _callCounter = fixture.Services.GetRequiredService<AnalyticsQueryCallCounter>();
    }

    public async Task InitializeAsync()
    {
        var userAId = await RegisterAsync(_emailA);
        var userBId = await RegisterAsync(_emailB);

        (_tenantAId, _tenantBId) = await ProvisionTenantsAndMembershipsAsync(userAId, userBId);

        _tokenA = await LoginAsync(_emailA);
        _tokenB = await LoginAsync(_emailB);

        await SeedFactSalesInvoiceItemAsync(_tenantAId, dateKey: 20260115, netAmount: 1000m);
        await SeedFactSalesInvoiceItemAsync(_tenantBId, dateKey: 20260115, netAmount: 2000m);

        _callCounter.Reset();
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        await using (var warehouseDb = await warehouseDbFactory.CreateDbContextAsync())
        {
            await warehouseDb.FactSalesInvoiceItems.Where(f => f.TenantId == _tenantAId || f.TenantId == _tenantBId).ExecuteDeleteAsync();
            await warehouseDb.DimCustomers.Where(c => c.TenantId == _tenantAId || c.TenantId == _tenantBId).ExecuteDeleteAsync();
            await warehouseDb.DimProducts.Where(p => p.TenantId == _tenantAId || p.TenantId == _tenantBId).ExecuteDeleteAsync();
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
    public async Task Query_RepeatedIdenticalRequest_IsServedFromCache()
    {
        var payload = new { dataset = "sales", metrics = new[] { "netRevenue" }, dimensions = new[] { "date.month" } };

        using var first = await SendQueryAsync(_tokenA, payload);
        first.EnsureSuccessStatusCode();

        using var second = await SendQueryAsync(_tokenA, payload);
        second.EnsureSuccessStatusCode();

        _callCounter.Count.Should().Be(1, "a segunda consulta idêntica deveria ser servida do cache, sem consultar o Warehouse de novo");
    }

    [Fact]
    public async Task Query_SameRequestDifferentTenants_NeverSharesCacheEntry()
    {
        var payload = new { dataset = "sales", metrics = new[] { "netRevenue" }, dimensions = new[] { "date.month" } };

        using var responseA = await SendQueryAsync(_tokenA, payload);
        responseA.EnsureSuccessStatusCode();

        using var responseB = await SendQueryAsync(_tokenB, payload);
        responseB.EnsureSuccessStatusCode();

        _callCounter.Count.Should().Be(2, "tenants diferentes nunca podem compartilhar a mesma entrada de cache, mesmo com a consulta idêntica");

        var bodyA = await responseA.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var bodyB = await responseB.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        bodyA.GetProperty("data")[0].GetProperty("netRevenue").GetDecimal().Should().Be(1000m);
        bodyB.GetProperty("data")[0].GetProperty("netRevenue").GetDecimal().Should().Be(2000m);
    }

    private Task<HttpResponseMessage> SendQueryAsync(string accessToken, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/query")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private async Task<Guid> RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName = email });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return Guid.Parse(DecodeSubClaim(body.GetProperty("accessToken").GetString()!));
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    /// <summary>Owner em ambos os tenants, para ter <c>analytics.query</c>.</summary>
    private async Task<(Guid TenantAId, Guid TenantBId)> ProvisionTenantsAndMembershipsAsync(Guid userAId, Guid userBId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenantA = Tenant.Create("Analytics Cache Tenant A (E1.4)", $"e1-4-a-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
        var tenantB = Tenant.Create("Analytics Cache Tenant B (E1.4)", $"e1-4-b-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

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

    private async Task SeedFactSalesInvoiceItemAsync(Guid tenantId, int dateKey, decimal netAmount)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();

        tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = await warehouseDbFactory.CreateDbContextAsync();

            var customer = DimCustomer.CreateCurrentVersion(
                tenantId, Guid.NewGuid(), $"AN-CACHE-C-{Guid.NewGuid():N}", "Cliente Cache",
                email: null, city: null, stateOrRegion: null, countryCode: null, isActive: true, DateTimeOffset.UtcNow);
            var product = DimProduct.CreateCurrentVersion(
                tenantId, Guid.NewGuid(), $"AN-CACHE-P-{Guid.NewGuid():N}", "Produto Cache", "Product",
                categoryKey: null, unitOfMeasure: null, isActive: true, DateTimeOffset.UtcNow);
            db.DimCustomers.Add(customer);
            db.DimProducts.Add(product);
            await db.SaveChangesAsync();

            var fact = FactSalesInvoiceItem.Create(
                tenantId,
                tenantKey: 1,
                companyKey: 1,
                dateKey: dateKey,
                customerKey: customer.CustomerKey,
                productKey: product.ProductKey,
                currencyKey: 1,
                sourceSystemId: Guid.NewGuid(),
                sourceEntity: "sales-invoices",
                sourceRecordId: $"NF-CACHE-{Guid.NewGuid():N}-1",
                salesInvoiceId: Guid.NewGuid(),
                salesInvoiceItemId: Guid.NewGuid(),
                rawObjectUri: $"tests/{Guid.NewGuid():N}.json",
                loadBatchId: Guid.NewGuid(),
                invoiceNumber: "NF-CACHE",
                status: "Issued",
                lineNumber: 1,
                quantity: 1m,
                grossAmount: netAmount,
                discountAmount: 0m,
                taxAmount: null,
                netAmount: netAmount);

            db.FactSalesInvoiceItems.Add(fact);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }
    }

    private static string DecodeSubClaim(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        using var document = System.Text.Json.JsonDocument.Parse(Base64UrlDecode(payloadSegment));
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
