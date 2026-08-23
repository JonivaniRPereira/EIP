using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Intelligence.Dashboard.Infrastructure;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E3.3 (`docs/roadmap/fase-2-backlog.md`): um Member recebe 403 ao
/// tentar publicar/arquivar um dashboard, mas consegue visualizá-lo — <c>dashboard.view</c> é
/// concedido a todo papel, <c>dashboard.manage</c> só a Owner/Admin (mesma política de
/// `ConnectorPermissions`/`MetricsPermissions`).
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class DashboardPermissionTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _emailOwner = $"dashboard-owner-{Guid.NewGuid():N}@test.com";
    private readonly string _emailMember = $"dashboard-member-{Guid.NewGuid():N}@test.com";

    private Guid _tenantId;
    private string _memberToken = null!;
    private Guid _dashboardId;

    public DashboardPermissionTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var ownerId = await RegisterAsync(_emailOwner);
        var memberId = await RegisterAsync(_emailMember);

        _tenantId = await ProvisionTenantAndMembershipsAsync(ownerId, memberId);

        _memberToken = await LoginAsync(_emailMember);

        _dashboardId = await SeedDashboardAsync(ownerId);
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var dashboardDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<DashboardDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        await using (var dashboardDb = await dashboardDbFactory.CreateDbContextAsync())
        {
            await dashboardDb.DashboardVersions.Where(v => v.TenantId == _tenantId).ExecuteDeleteAsync();
            await dashboardDb.Dashboards.Where(d => d.TenantId == _tenantId).ExecuteDeleteAsync();
        }

        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
        await using (var tenantDb = await tenantDbFactory.CreateDbContextAsync())
        {
            await tenantDb.Memberships.Where(m => m.TenantId == _tenantId).ExecuteDeleteAsync();
            await tenantDb.Tenants.Where(t => t.Id == _tenantId).ExecuteDeleteAsync();
        }

        tenantContextAccessor.Current = null;

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _emailOwner || u.Email == _emailMember).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task GetById_AsMember_Succeeds()
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/dashboards/{_dashboardId}", _memberToken);
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Publish_AsMember_ReturnsForbidden()
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/dashboards/{_dashboardId}/publish", _memberToken);
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Archive_AsMember_ReturnsForbidden()
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/dashboards/{_dashboardId}/archive", _memberToken);
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AsMember_ReturnsForbidden()
    {
        using var request = CreateRequest(HttpMethod.Patch, $"/api/v1/dashboards/{_dashboardId}", _memberToken);
        request.Content = JsonContent.Create(new { name = "Tentativa de edição" });

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private async Task<Guid> ProvisionTenantAndMembershipsAsync(Guid ownerId, Guid memberId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenant = Tenant.Create("Dashboard Permission Tenant (E3.3)", $"e3-3-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

        tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await tenantDbFactory.CreateDbContextAsync();
            var ownerMembership = Membership.Create(ownerId, tenant.Id, MembershipRole.Owner);
            var memberMembership = Membership.Create(memberId, tenant.Id, MembershipRole.Member);
            ownerMembership.Activate();
            memberMembership.Activate();

            db.Tenants.Add(tenant);
            db.Memberships.AddRange(ownerMembership, memberMembership);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }

        return tenant.Id;
    }

    private async Task<Guid> SeedDashboardAsync(Guid ownerId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var dashboardDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<DashboardDbContext>>();

        var dashboard = EIP.Intelligence.Dashboard.Domain.Dashboard.Create(_tenantId, "Painel Compartilhado (E3.3)", ownerId);

        tenantContextAccessor.Current = new TenantContext(_tenantId);
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
