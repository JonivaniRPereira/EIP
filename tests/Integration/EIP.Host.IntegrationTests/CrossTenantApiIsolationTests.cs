using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// E2.6 — prova, batendo no Host real via HTTP (não em nível de banco, que já é coberto por
/// EIP.Platform.Tenant.Infrastructure.IntegrationTests), que um usuário do tenant A nunca lê um
/// recurso do tenant B mesmo adulterando o ID no payload/rota (docs/08-Multi-Tenant.md §13).
///
/// Roda contra um Host real + SQL Server efêmero (<see cref="HostApiFixture"/>, Testcontainers) —
/// nada de infraestrutura pré-existente precisa estar de pé, nem local nem no CI (E5).
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class CrossTenantApiIsolationTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _emailA = $"user-a-{Guid.NewGuid():N}@test.com";
    private readonly string _emailB = $"user-b-{Guid.NewGuid():N}@test.com";

    private Guid _tenantAId;
    private Guid _tenantBId;
    private string _tokenA = null!;
    private string _tokenB = null!;

    public CrossTenantApiIsolationTests(HostApiFixture fixture)
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
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        tenantContextAccessor.Current = TenantContext.System;
        await using (var db = await tenantDbFactory.CreateDbContextAsync())
        {
            await db.Memberships.Where(m => m.TenantId == _tenantAId || m.TenantId == _tenantBId).ExecuteDeleteAsync();
            await db.Tenants.Where(t => t.Id == _tenantAId || t.Id == _tenantBId).ExecuteDeleteAsync();
        }

        tenantContextAccessor.Current = null;

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _emailA || u.Email == _emailB).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task GetTenant_WithOwnTenantId_ReturnsOk()
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/tenants/{_tenantAId}", _tokenA);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenant_WithOtherTenantsId_ReturnsForbidden_EvenWithAdulteratedIdInRoute()
    {
        // Usuário A autenticado tentando ler o tenant B só trocando o ID na URL — não deve nem
        // vazar dados nem 200 silenciosamente; tem que ser bloqueado explicitamente.
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/tenants/{_tenantBId}", _tokenA);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTenant_WithoutToken_ReturnsUnauthorized()
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/tenants/{_tenantAId}", accessToken: null);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SelectTenant_ForTenantUserDoesNotBelongTo_Fails()
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/auth/select-tenant", _tokenA);
        request.Content = JsonContent.Create(new { tenantId = _tenantBId });

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

    /// <summary>Não existe endpoint de provisionamento de tenant/membership na Fase 0 — cria direto
    /// via EF Core, com a mesma sentinela de sistema usada pelo MembershipDirectory.</summary>
    private async Task<(Guid TenantAId, Guid TenantBId)> ProvisionTenantsAndMembershipsAsync(Guid userAId, Guid userBId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenantA = EIP.Platform.Tenant.Domain.Tenant.Create("Tenant A (E2.6)", $"e2e-a-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
        var tenantB = EIP.Platform.Tenant.Domain.Tenant.Create("Tenant B (E2.6)", $"e2e-b-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

        tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await tenantDbFactory.CreateDbContextAsync();
            var membershipA = Membership.Create(userAId, tenantA.Id, MembershipRole.Member);
            var membershipB = Membership.Create(userBId, tenantB.Id, MembershipRole.Member);
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
