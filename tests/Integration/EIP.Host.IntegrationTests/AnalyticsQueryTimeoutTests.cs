using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Semantic.Application;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o guardrail de timeout de execução do E1.5 (`docs/roadmap/fase-2-backlog.md`) — precisa de
/// uma <see cref="WebApplicationFactory{Program}"/> própria, separada da <see cref="HostApiFixture"/>
/// compartilhada pelo resto da coleção: um `Analytics:QueryTimeoutMilliseconds` bem curto (50ms) só
/// para esta suíte, combinado com um <see cref="SlowAnalyticsQueryService"/> que atrasa
/// deliberadamente 300ms — determinístico independente da velocidade da máquina, já que o timeout só
/// envolve a chamada ao Warehouse, nunca o pipeline HTTP/autenticação em volta. Reaproveita o mesmo
/// SQL Server e Redis efêmeros já de pé na <see cref="HostApiFixture"/> (não sobe um terceiro
/// container só para isto).
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class AnalyticsQueryTimeoutTests : IAsyncLifetime
{
    private const string Password = "SenhaForte!123";

    private readonly HostApiFixture _sharedFixture;
    private readonly string _email = $"analytics-timeout-{Guid.NewGuid():N}@test.com";

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private string _token = null!;

    public AnalyticsQueryTimeoutTests(HostApiFixture sharedFixture)
    {
        _sharedFixture = sharedFixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHostBuilder =>
        {
            webHostBuilder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("ConnectionStrings:TenantDb", _sharedFixture.ConnectionString),
                new("ConnectionStrings:UsageDb", _sharedFixture.ConnectionString),
                new("ConnectionStrings:IdentityDb", _sharedFixture.ConnectionString),
                new("ConnectionStrings:ConnectorDb", _sharedFixture.ConnectionString),
                new("ConnectionStrings:CanonicalDb", _sharedFixture.ConnectionString),
                new("ConnectionStrings:WarehouseDb", _sharedFixture.ConnectionString),
                new("ConnectionStrings:Redis", _sharedFixture.RedisConnectionString),
                new("ConnectionStrings:RabbitMQ", "amqp://guest:guest@localhost:16599/"),
                new("Analytics:QueryTimeoutMilliseconds", "50"),
            ]));

            webHostBuilder.ConfigureTestServices(services =>
            {
                services.AddScoped<AnalyticsQueryService>();
                services.AddScoped<IAnalyticsQueryService>(sp => new SlowAnalyticsQueryService(
                    sp.GetRequiredService<AnalyticsQueryService>(),
                    TimeSpan.FromMilliseconds(300)));
            });
        });

        _client = _factory.CreateClient();

        var userId = await RegisterAsync(_email);
        _tenantId = await ProvisionTenantAndMembershipAsync(userId);
        _token = await LoginAsync(_email);
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _factory.Services.GetRequiredService<ITenantContextAccessor>();

        var tenantDbFactory = _factory.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
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

        await using var identityDb = _factory.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<EIP.Platform.Identity.Infrastructure.AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _email).ExecuteDeleteAsync();

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Query_ExceedingExecutionTimeout_ReturnsBadRequestExplainingTheLimit()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/query")
        {
            Content = JsonContent.Create(new
            {
                dataset = "sales",
                metrics = new[] { "netRevenue" },
                dimensions = new[] { "date.month" },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().Contain("tempo limite");
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
        var tenantContextAccessor = _factory.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _factory.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenant = Tenant.Create("Analytics Timeout Tenant (E1.5)", $"e1-5-timeout-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

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

/// <summary>Decora <see cref="IAnalyticsQueryService"/> com um atraso deliberado, só para provar o
/// guardrail de timeout (E1.5) de forma determinística — nunca usado fora deste teste.</summary>
internal sealed class SlowAnalyticsQueryService : IAnalyticsQueryService
{
    private readonly IAnalyticsQueryService _inner;
    private readonly TimeSpan _delay;

    public SlowAnalyticsQueryService(IAnalyticsQueryService inner, TimeSpan delay)
    {
        _inner = inner;
        _delay = delay;
    }

    public async Task<IReadOnlyList<AnalyticsDimensionRow>> QueryCommercialByDimensionAsync(AnalyticsQueryFilter filter, CancellationToken cancellationToken)
    {
        await Task.Delay(_delay, cancellationToken);
        return await _inner.QueryCommercialByDimensionAsync(filter, cancellationToken);
    }

    public Task<DateTimeOffset?> GetDataFreshnessAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _inner.GetDataFreshnessAsync(tenantId, cancellationToken);
}
