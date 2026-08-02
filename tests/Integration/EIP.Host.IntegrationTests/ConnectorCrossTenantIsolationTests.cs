using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Domain;
using EIP.Data.Connector.Infrastructure;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Mesmo gate de isolamento cross-tenant de <see cref="CrossTenantApiIsolationTests"/> (E2.6), agora
/// para os recursos do módulo Connector introduzidos em E7 — provando que o critério de saída da
/// Fase 0 ("usuário de tenant A não acessa recursos de tenant B") continua valendo para todo domínio
/// novo, não só o que existia quando o critério foi escrito originalmente.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class ConnectorCrossTenantIsolationTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _emailA = $"connector-a-{Guid.NewGuid():N}@test.com";
    private readonly string _emailB = $"connector-b-{Guid.NewGuid():N}@test.com";

    private Guid _tenantAId;
    private Guid _tenantBId;
    private string _tokenA = null!;
    private Guid _connectorInstanceBId;
    private Guid _syncRunBId;

    public ConnectorCrossTenantIsolationTests(HostApiFixture fixture)
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

        (_connectorInstanceBId, _syncRunBId) = await SeedConnectorInstanceAndRunForTenantBAsync();
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var connectorDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<ConnectorDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        await using (var connectorDb = await connectorDbFactory.CreateDbContextAsync())
        {
            await connectorDb.SyncRuns.Where(r => r.TenantId == _tenantAId || r.TenantId == _tenantBId).ExecuteDeleteAsync();
            await connectorDb.ConnectorInstances.Where(i => i.TenantId == _tenantAId || i.TenantId == _tenantBId).ExecuteDeleteAsync();
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
    public async Task RequestSync_ForOtherTenantsConnectorInstance_DoesNotSucceed_EvenWithAdulteratedIdInRoute()
    {
        // Usuário A autenticado tentando sincronizar a instância do tenant B só trocando o ID na
        // rota — RLS já esconde a linha; a API não pode responder 202 (aceito) para isso.
        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/connectors/{_connectorInstanceBId}/sync", _tokenA);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetSyncRun_ForOtherTenantsRun_ReturnsNotFound_EvenWithAdulteratedIdInRoute()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/connectors/{_connectorInstanceBId}/sync-runs/{_syncRunBId}",
            _tokenA);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegisterConnector_WithoutToken_ReturnsUnauthorized()
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/connectors", accessToken: null);
        request.Content = JsonContent.Create(new { name = "x", baseUrl = "http://localhost/x" });

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

    /// <summary>Owner em ambos os tenants, para ter connector.manage/connector.view.</summary>
    private async Task<(Guid TenantAId, Guid TenantBId)> ProvisionTenantsAndMembershipsAsync(Guid userAId, Guid userBId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenantA = EIP.Platform.Tenant.Domain.Tenant.Create("Connector Tenant A (E7)", $"e7-a-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
        var tenantB = EIP.Platform.Tenant.Domain.Tenant.Create("Connector Tenant B (E7)", $"e7-b-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

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

    /// <summary>Sem envolver RabbitMQ (indisponível nesta fixture) — semeia direto via EF Core, mesmo
    /// padrão de <see cref="CrossTenantApiIsolationTests"/> para Tenant/Membership.</summary>
    private async Task<(Guid ConnectorInstanceId, Guid SyncRunId)> SeedConnectorInstanceAndRunForTenantBAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var connectorDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<ConnectorDbContext>>();

        var instance = ConnectorInstance.Create(_tenantBId, Guid.NewGuid(), "Conector B (E7)", "http://localhost/sample", "customers");
        var run = SyncRun.CreatePending(_tenantBId, instance.Id, Guid.NewGuid().ToString());

        tenantContextAccessor.Current = new TenantContext(_tenantBId);
        try
        {
            await using var db = await connectorDbFactory.CreateDbContextAsync();
            db.ConnectorInstances.Add(instance);
            db.SyncRuns.Add(run);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }

        return (instance.Id, run.Id);
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
