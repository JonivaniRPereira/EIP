using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Warehouse.Domain;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Intelligence.Dashboard.Infrastructure;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E3.2 (`docs/roadmap/fase-2-backlog.md`): o resultado de
/// <c>POST /api/v1/dashboards/{id}/query</c> bate exatamente com o mesmo <c>QueryDefinition</c>
/// executado direto via <c>POST /api/v1/analytics/query</c> — mesmo caminho de código, só orquestrado
/// por dashboard/widget em vez de chamado solto.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class DashboardQueryTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _email = $"dashboard-query-{Guid.NewGuid():N}@test.com";

    private Guid _tenantId;
    private string _token = null!;
    private Guid _dashboardId;

    public DashboardQueryTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var userId = await RegisterAsync(_email);
        _tenantId = await ProvisionTenantAndMembershipAsync(userId);
        _token = await LoginAsync(_email);

        await SeedFactSalesInvoiceItemAsync(_tenantId, dateKey: 20260115, netAmount: 1500m);
        await SeedFactSalesInvoiceItemAsync(_tenantId, dateKey: 20260215, netAmount: 2500m);

        _dashboardId = await CreateAndPublishDashboardAsync();
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var dashboardDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<DashboardDbContext>>();
        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        await using (var dashboardDb = await dashboardDbFactory.CreateDbContextAsync())
        {
            await dashboardDb.DashboardVersions.Where(v => v.TenantId == _tenantId).ExecuteDeleteAsync();
            await dashboardDb.Dashboards.Where(d => d.TenantId == _tenantId).ExecuteDeleteAsync();
        }

        await using (var warehouseDb = await warehouseDbFactory.CreateDbContextAsync())
        {
            await warehouseDb.FactSalesInvoiceItems.Where(f => f.TenantId == _tenantId).ExecuteDeleteAsync();
            await warehouseDb.DimCustomers.Where(c => c.TenantId == _tenantId).ExecuteDeleteAsync();
            await warehouseDb.DimProducts.Where(p => p.TenantId == _tenantId).ExecuteDeleteAsync();
        }

        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
        await using (var tenantDb = await tenantDbFactory.CreateDbContextAsync())
        {
            await tenantDb.Memberships.Where(m => m.TenantId == _tenantId).ExecuteDeleteAsync();
            await tenantDb.Tenants.Where(t => t.Id == _tenantId).ExecuteDeleteAsync();
        }

        tenantContextAccessor.Current = null;

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<EIP.Platform.Identity.Infrastructure.AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _email).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Query_MatchesTheSameQueryDefinitionExecutedDirectlyViaAnalyticsEngine()
    {
        using var dashboardResponse = await SendAsync(HttpMethod.Post, $"/api/v1/dashboards/{_dashboardId}/query");
        dashboardResponse.EnsureSuccessStatusCode();
        var dashboardBody = await dashboardResponse.Content.ReadFromJsonAsync<JsonElement>();

        dashboardBody.GetArrayLength().Should().Be(1, "o dashboard tem exatamente 1 widget");
        var widgetResult = dashboardBody[0];
        widgetResult.GetProperty("widgetTitle").GetString().Should().Be("Receita Líquida por Mês");

        using var directRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/query")
        {
            Content = JsonContent.Create(new { dataset = "sales", metrics = new[] { "netRevenue" }, dimensions = new[] { "date.month" } }),
        };
        directRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var directResponse = await _client.SendAsync(directRequest);
        directResponse.EnsureSuccessStatusCode();
        var directBody = await directResponse.Content.ReadFromJsonAsync<JsonElement>();

        var dashboardData = widgetResult.GetProperty("data");
        var directData = directBody.GetProperty("data");

        dashboardData.GetArrayLength().Should().Be(directData.GetArrayLength()).And.Be(2);

        var dashboardTotal = dashboardData.EnumerateArray().Sum(row => row.GetProperty("netRevenue").GetDecimal());
        var directTotal = directData.EnumerateArray().Sum(row => row.GetProperty("netRevenue").GetDecimal());
        dashboardTotal.Should().Be(directTotal).And.Be(4000m);

        var dashboardMetadata = widgetResult.GetProperty("metadata");
        var directMetadata = directBody.GetProperty("metadata");
        dashboardMetadata.GetProperty("dataset").GetString().Should().Be(directMetadata.GetProperty("dataset").GetString());
        dashboardMetadata.GetProperty("rowCount").GetInt32().Should().Be(directMetadata.GetProperty("rowCount").GetInt32());
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return _client.SendAsync(request);
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

    private async Task<Guid> ProvisionTenantAndMembershipAsync(Guid userId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenant = Tenant.Create("Dashboard Query Tenant (E3.2)", $"e3-2-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

        tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await tenantDbFactory.CreateDbContextAsync();
            var membership = Membership.Create(userId, tenant.Id, MembershipRole.Owner);
            membership.Activate();

            db.Tenants.Add(tenant);
            db.Memberships.Add(membership);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }

        return tenant.Id;
    }

    private async Task<Guid> CreateAndPublishDashboardAsync()
    {
        using var createResponse = await SendJsonAsync(HttpMethod.Post, "/api/v1/dashboards", new { name = "Painel Comercial (E3.2)" });
        createResponse.EnsureSuccessStatusCode();
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var dashboardId = createBody.GetProperty("id").GetGuid();

        var widget = new
        {
            type = "Kpi",
            title = "Receita Líquida por Mês",
            queryDefinition = new { dataset = "sales", metrics = new[] { "netRevenue" }, dimensions = new[] { "date.month" } },
        };
        using var updateResponse = await SendJsonAsync(HttpMethod.Patch, $"/api/v1/dashboards/{dashboardId}", new { widgets = new[] { widget } });
        updateResponse.EnsureSuccessStatusCode();

        using var publishResponse = await SendAsync(HttpMethod.Post, $"/api/v1/dashboards/{dashboardId}/publish");
        publishResponse.EnsureSuccessStatusCode();

        return dashboardId;
    }

    private Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, object payload)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return _client.SendAsync(request);
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
                tenantId, Guid.NewGuid(), $"DASH-C-{Guid.NewGuid():N}", "Cliente Dashboard",
                email: null, city: null, stateOrRegion: null, countryCode: null, isActive: true, DateTimeOffset.UtcNow);
            var product = DimProduct.CreateCurrentVersion(
                tenantId, Guid.NewGuid(), $"DASH-P-{Guid.NewGuid():N}", "Produto Dashboard", "Product",
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
                sourceRecordId: $"NF-DASH-{Guid.NewGuid():N}-1",
                salesInvoiceId: Guid.NewGuid(),
                salesInvoiceItemId: Guid.NewGuid(),
                rawObjectUri: $"tests/{Guid.NewGuid():N}.json",
                loadBatchId: Guid.NewGuid(),
                invoiceNumber: "NF-DASH",
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
