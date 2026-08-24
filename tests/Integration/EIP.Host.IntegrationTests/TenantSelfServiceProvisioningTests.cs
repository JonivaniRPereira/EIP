using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E1.1/E1.2 da Fase 3 (`docs/roadmap/fase-3-backlog.md`): um usuário
/// recém-registrado, sem nenhum tenant, consegue criar um via <c>POST /api/v1/tenants</c> e
/// imediatamente fazer login nele sem qualquer etapa manual/SQL — fecha a lacuna que forçou toda
/// validação ao vivo desde a Fase 0 a provisionar tenants via `sqlcmd` sob a sentinela de sistema.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class TenantSelfServiceProvisioningTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _email = $"tenant-self-service-{Guid.NewGuid():N}@test.com";
    private readonly string _slug = $"self-service-{Guid.NewGuid():N}";

    private Guid? _createdTenantId;

    public TenantSelfServiceProvisioningTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_createdTenantId is { } tenantId)
        {
            var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
            var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
            tenantContextAccessor.Current = TenantContext.System;
            try
            {
                await using var db = await tenantDbFactory.CreateDbContextAsync();
                await db.Memberships.Where(m => m.TenantId == tenantId).ExecuteDeleteAsync();
                await db.Companies.Where(c => c.TenantId == tenantId).ExecuteDeleteAsync();
                await db.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync();
            }
            finally
            {
                tenantContextAccessor.Current = null;
            }
        }

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _email).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Create_ByFreshlyRegisteredUserWithNoTenant_AllowsImmediateLoginWithoutAnyManualStep()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = _email, password = Password, displayName = _email });
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = registerBody.GetProperty("accessToken").GetString()!;

        registerBody.GetProperty("requiresTenantSelection").GetBoolean().Should().BeTrue("um usuário novo não pertence a nenhum tenant ainda");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(new { name = "Minha Empresa", slug = _slug, countryCode = "BR" }),
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var createResponse = await _client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        _createdTenantId = createBody.GetProperty("tenantId").GetGuid();
        createBody.GetProperty("slug").GetString().Should().Be(_slug);

        // Sem nenhuma etapa manual/SQL: um login de verdade já reflete a membership recém-criada.
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = _email, password = Password });
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();

        loginBody.GetProperty("requiresTenantSelection").GetBoolean().Should().BeFalse("com exatamente 1 membership, o tenant é selecionado automaticamente");
    }

    [Fact]
    public async Task Create_WithDuplicateSlug_ReturnsConflictNeverServerError()
    {
        var emailA = $"tenant-dup-a-{Guid.NewGuid():N}@test.com";
        var emailB = $"tenant-dup-b-{Guid.NewGuid():N}@test.com";
        var duplicateSlug = $"dup-{Guid.NewGuid():N}";

        var tokenA = await RegisterAndGetAccessTokenAsync(emailA);
        var tokenB = await RegisterAndGetAccessTokenAsync(emailB);

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(new { name = "Primeira", slug = duplicateSlug, countryCode = "BR" }),
        };
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        using var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        _createdTenantId = firstBody.GetProperty("tenantId").GetGuid();

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(new { name = "Segunda", slug = duplicateSlug, countryCode = "BR" }),
        };
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        using var secondResponse = await _client.SendAsync(secondRequest);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == emailB).ExecuteDeleteAsync();
    }

    private async Task<string> RegisterAndGetAccessTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName = email });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }
}
