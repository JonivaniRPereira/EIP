using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Intelligence.Dashboard.Domain;
using EIP.Intelligence.Dashboard.Infrastructure;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Mesmo gate de isolamento cross-tenant de <see cref="ConnectorCrossTenantIsolationTests"/> (E7),
/// agora para os recursos do módulo Dashboard introduzidos em E3.1 — usuário de tenant A não
/// acessa/edita/publica/arquiva recursos de tenant B, mesmo adulterando o Id na rota.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class DashboardCrossTenantIsolationTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _emailA = $"dashboard-a-{Guid.NewGuid():N}@test.com";
    private readonly string _emailB = $"dashboard-b-{Guid.NewGuid():N}@test.com";

    private Guid _tenantAId;
    private Guid _tenantBId;
    private string _tokenA = null!;
    private Guid _dashboardBId;

    public DashboardCrossTenantIsolationTests(HostApiFixture fixture)
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

        _dashboardBId = await SeedDashboardForTenantBAsync(userBId);
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var dashboardDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<DashboardDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        await using (var dashboardDb = await dashboardDbFactory.CreateDbContextAsync())
        {
            await dashboardDb.DashboardVersions.Where(v => v.TenantId == _tenantAId || v.TenantId == _tenantBId).ExecuteDeleteAsync();
            await dashboardDb.Dashboards.Where(d => d.TenantId == _tenantAId || d.TenantId == _tenantBId).ExecuteDeleteAsync();
        }

        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
        await using (var tenantDb = await tenantDbFactory.CreateDbContextAsync())
        {
            await tenantDb.Memberships.Where(m => m.TenantId == _tenantAId || m.TenantId == _tenantBId).ExecuteDeleteAsync();
            await tenantDb.Tenants.Where(t => t.Id == _tenantAId || t.Id == _tenantBId).ExecuteDeleteAsync();
        }

        tenantContextAccessor.Current = null;

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _emailA || u.Email == _emailB).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task GetById_ForOtherTenantsDashboard_ReturnsNotFound_EvenWithAdulteratedIdInRoute()
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/dashboards/{_dashboardBId}", _tokenA);
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_NeverIncludesOtherTenantsDashboards()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/dashboards", _tokenA);
        using var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().Should().NotContain(d => d.GetProperty("id").GetGuid() == _dashboardBId);
    }

    [Fact]
    public async Task Update_ForOtherTenantsDashboard_ReturnsNotFound_EvenWithAdulteratedIdInRoute()
    {
        using var request = CreateRequest(HttpMethod.Patch, $"/api/v1/dashboards/{_dashboardBId}", _tokenA);
        request.Content = JsonContent.Create(new { name = "Invasão" });

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Publish_ForOtherTenantsDashboard_ReturnsNotFound_EvenWithAdulteratedIdInRoute()
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/dashboards/{_dashboardBId}/publish", _tokenA);
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Archive_ForOtherTenantsDashboard_ReturnsNotFound_EvenWithAdulteratedIdInRoute()
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/dashboards/{_dashboardBId}/archive", _tokenA);
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/dashboards", accessToken: null);
        request.Content = JsonContent.Create(new { name = "x" });

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    /// <summary>Owner em ambos os tenants, para ter dashboard.manage/dashboard.view.</summary>
    private async Task<(Guid TenantAId, Guid TenantBId)> ProvisionTenantsAndMembershipsAsync(Guid userAId, Guid userBId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenantA = Tenant.Create("Dashboard Tenant A (E3.1)", $"e3-1-a-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
        var tenantB = Tenant.Create("Dashboard Tenant B (E3.1)", $"e3-1-b-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

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

    private async Task<Guid> SeedDashboardForTenantBAsync(Guid ownerId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var dashboardDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<DashboardDbContext>>();

        var dashboard = EIP.Intelligence.Dashboard.Domain.Dashboard.Create(_tenantBId, "Painel do Tenant B (E3.1)", ownerId);

        tenantContextAccessor.Current = new TenantContext(_tenantBId);
        try
        {
            await using var db = await dashboardDbFactory.CreateDbContextAsync();
            db.Dashboards.Add(dashboard);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }

        return dashboard.Id;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

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

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
