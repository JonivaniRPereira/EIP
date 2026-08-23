using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E1.3 (docs/roadmap/fase-2-backlog.md): <c>GET
/// /api/v1/analytics/datasets</c>/<c>GET /api/v1/analytics/datasets/{id}</c> refletem exatamente o
/// catálogo publicado em código (<c>AnalyticsCatalog</c>), sem hardcode duplicado no cliente — mudar
/// uma definição no backend muda a resposta sem exigir alteração no frontend.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class AnalyticsDatasetsTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _email = $"analytics-catalog-{Guid.NewGuid():N}@test.com";

    private Guid _tenantId;
    private string _token = null!;

    public AnalyticsDatasetsTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var userId = await RegisterAsync(_email);
        _tenantId = await ProvisionTenantAndMembershipAsync(userId);
        _token = await LoginAsync(_email);
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await tenantDbFactory.CreateDbContextAsync();
            await db.Memberships.Where(m => m.TenantId == _tenantId).ExecuteDeleteAsync();
            await db.Tenants.Where(t => t.Id == _tenantId).ExecuteDeleteAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<EIP.Platform.Identity.Infrastructure.AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _email).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ListDatasets_ReturnsSalesDatasetWithMetricsAndDimensions()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/analytics/datasets");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetArrayLength().Should().Be(1);
        var sales = body[0];
        sales.GetProperty("name").GetString().Should().Be("sales");
        sales.GetProperty("owner").GetString().Should().Be("Comercial");
        sales.GetProperty("semanticVersion").GetString().Should().Be("1.0");

        var metricNames = sales.GetProperty("metrics").EnumerateArray().Select(m => m.GetProperty("name").GetString()).ToList();
        metricNames.Should().BeEquivalentTo(["netRevenue", "invoicedQuantity", "averageTicket"]);

        var dimensionNames = sales.GetProperty("dimensions").EnumerateArray().Select(d => d.GetProperty("name").GetString()).ToList();
        dimensionNames.Should().BeEquivalentTo(["date.month", "customer", "product"]);
    }

    [Fact]
    public async Task GetDataset_WithKnownId_ReturnsMatchingDataset()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/analytics/datasets/sales");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("name").GetString().Should().Be("sales");
    }

    [Fact]
    public async Task GetDataset_WithUnknownId_ReturnsNotFound()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/v1/analytics/datasets/financial");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    /// <summary>Owner no tenant, para ter <c>analytics.query</c>.</summary>
    private async Task<Guid> ProvisionTenantAndMembershipAsync(Guid userId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenant = Tenant.Create("Analytics Catalog Tenant (E1.3)", $"e1-3-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

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
