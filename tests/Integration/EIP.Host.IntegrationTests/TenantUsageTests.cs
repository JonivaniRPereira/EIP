using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using EIP.Data.DataLake;
using EIP.Platform.Administration.Infrastructure;
using EIP.Platform.Identity.Domain;
using EIP.Shared.Contracts.Administration;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

[Collection(HostApiCollection.Name)]
public sealed class TenantUsageTests(HostApiFixture fixture) : IAsyncLifetime
{
    private readonly IContainer _storage = new ContainerBuilder("adobe/s3mock:5.1.0")
        .WithPortBinding(9090, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(9090))
        .Build();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _storage.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("ConnectionStrings:TenantDb", fixture.ConnectionString),
                new("ConnectionStrings:IdentityDb", fixture.ConnectionString),
                new("ConnectionStrings:ConnectorDb", fixture.ConnectionString),
                new("ConnectionStrings:CanonicalDb", fixture.ConnectionString),
                new("ConnectionStrings:WarehouseDb", fixture.ConnectionString),
                new("ConnectionStrings:DashboardDb", fixture.ConnectionString),
                new("ConnectionStrings:UsageDb", fixture.ConnectionString),
                new("ConnectionStrings:Redis", fixture.RedisConnectionString),
                new("DataLake:ServiceUrl", $"http://{_storage.Hostname}:{_storage.GetMappedPublicPort(9090)}"),
                new("DataLake:BucketName", "usage-tests"),
                new("Connector:SyncQuota:PermitLimit", "10"),
            ]));
            builder.ConfigureTestServices(services => services.AddSingleton<IConnectorSyncPublisher, NoOpPublisher>());
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _storage.DisposeAsync();
    }

    [Fact]
    public async Task Usage_MatchesRealActivity_IsPlatformOnly_AndHonorsPeriod()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        var a = await ProvisionAsync();
        var b = await ProvisionAsync();
        using var clientA = a.Client;
        using var clientB = b.Client;
        using var anonymous = _factory.CreateClient();
        var url = UsageUrl(a.TenantId, from, to);
        using var deniedAnonymous = await anonymous.GetAsync(url);
        deniedAnonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var deniedOwner = await clientA.GetAsync(url);
        deniedOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var deniedOtherTenant = await clientA.GetAsync(UsageUrl(b.TenantId, from, to));
        deniedOtherTenant.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SyncAsync(a, 2);
        await SyncAsync(b, 1);
        for (var i = 0; i < 3; i++)
        {
            using var query = await clientA.PostAsJsonAsync("/api/v1/analytics/query", new
            {
                dataset = "sales",
                metrics = new[] { "netRevenue" },
                dimensions = new[] { "date.month" },
            });
            query.EnsureSuccessStatusCode();
        }

        using var invalidQuery = await clientA.PostAsJsonAsync("/api/v1/analytics/query", new { dataset = "missing" });
        invalidQuery.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = _factory.Services.GetRequiredService<IRawObjectStore>();
        await raw.PutAsync(new RawObjectMetadata(a.TenantId, Guid.NewGuid(), "customers", null, null, DateTimeOffset.UtcNow), "abc"u8.ToArray(), CancellationToken.None);
        await raw.PutAsync(new RawObjectMetadata(a.TenantId, Guid.NewGuid(), "customers", null, null, DateTimeOffset.UtcNow), "12345"u8.ToArray(), CancellationToken.None);
        await raw.PutAsync(new RawObjectMetadata(b.TenantId, Guid.NewGuid(), "customers", null, null, DateTimeOffset.UtcNow), "other"u8.ToArray(), CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(a.Email))!;
        var permission = new Claim(PlatformPermissions.ClaimType, PlatformPermissions.TenantUsageRead);
        (await users.AddClaimAsync(user, permission)).Succeeded.Should().BeTrue();
        using var report = await clientA.GetAsync(url);
        report.EnsureSuccessStatusCode();
        var usage = await report.Content.ReadFromJsonAsync<JsonElement>();
        usage.GetProperty("synchronizations").GetInt64().Should().Be(2);
        usage.GetProperty("analyticsQueries").GetInt64().Should().Be(3, "cache hits contam como novas consultas e consultas inválidas não contam");
        usage.GetProperty("dataLakeBytes").GetInt64().Should().Be(8);
        usage.GetProperty("dataLakeObjects").GetInt64().Should().Be(2);

        using var otherReport = await clientA.GetAsync(UsageUrl(b.TenantId, from, to));
        otherReport.EnsureSuccessStatusCode();
        var otherUsage = await otherReport.Content.ReadFromJsonAsync<JsonElement>();
        otherUsage.GetProperty("synchronizations").GetInt64().Should().Be(1);
        otherUsage.GetProperty("analyticsQueries").GetInt64().Should().Be(0);
        otherUsage.GetProperty("dataLakeBytes").GetInt64().Should().Be(5);
        using var emptyPeriod = await clientA.GetAsync(UsageUrl(a.TenantId, from.AddDays(-2), from.AddDays(-1)));
        emptyPeriod.EnsureSuccessStatusCode();
        var empty = await emptyPeriod.Content.ReadFromJsonAsync<JsonElement>();
        empty.GetProperty("synchronizations").GetInt64().Should().Be(0);
        empty.GetProperty("analyticsQueries").GetInt64().Should().Be(0);
        empty.GetProperty("dataLakeBytes").GetInt64().Should().Be(0);

        using var invalidPeriod = await clientA.GetAsync(UsageUrl(a.TenantId, to, from));
        invalidPeriod.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var missing = await clientA.GetAsync(UsageUrl(Guid.NewGuid(), from, to));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var systemTenant = await clientA.GetAsync(UsageUrl(TenantContext.SystemTenantId, from, to));
        systemTenant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await users.RemoveClaimAsync(user, permission)).Succeeded.Should().BeTrue();
        using var revoked = await clientA.GetAsync(url);
        revoked.StatusCode.Should().Be(HttpStatusCode.Forbidden, "revogação deve valer mesmo usando o token anterior");
    }

    [Fact]
    public async Task AnalyticsUsage_RlsFiltersAndBlocksOtherTenant_AndReaderRestoresContext()
    {
        var a = await ProvisionAsync();
        var b = await ProvisionAsync();
        using var clientA = a.Client;
        using var clientB = b.Client;
        var accessor = _factory.Services.GetRequiredService<ITenantContextAccessor>();
        var factory = _factory.Services.GetRequiredService<IDbContextFactory<UsageDbContext>>();
        try
        {
            accessor.Current = new TenantContext(a.TenantId);
            await using (var db = await factory.CreateDbContextAsync())
            {
                db.AnalyticsQueries.Add(new AnalyticsUsageEvent { Id = Guid.NewGuid(), TenantId = a.TenantId, OccurredAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }

            accessor.Current = new TenantContext(b.TenantId);
            await using (var db = await factory.CreateDbContextAsync())
            {
                (await db.AnalyticsQueries.CountAsync(e => e.TenantId == a.TenantId)).Should().Be(0);
                db.AnalyticsQueries.Add(new AnalyticsUsageEvent { Id = Guid.NewGuid(), TenantId = a.TenantId, OccurredAt = DateTimeOffset.UtcNow });
                var insert = async () => await db.SaveChangesAsync();
                await insert.Should().ThrowAsync<DbUpdateException>();
            }

            using var scope = _factory.Services.CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<EIP.Platform.Administration.Application.ITenantUsageReader>();
            var original = accessor.Current;
            await reader.ReadAsync(a.TenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);
            accessor.Current.Should().Be(original);
            accessor.Current = null;
            await using var unscoped = await factory.CreateDbContextAsync();
            (await unscoped.AnalyticsQueries.CountAsync()).Should().Be(0);
        }
        finally
        {
            accessor.Current = null;
        }
    }

    private async Task<(HttpClient Client, Guid TenantId, Guid CompanyId, string Email)> ProvisionAsync()
    {
        var client = _factory.CreateClient();
        var email = $"usage-{Guid.NewGuid():N}@test.local";
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "UsageTest!123", displayName = "Usage" });
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        using var provision = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Usage", slug = $"usage-{Guid.NewGuid():N}", countryCode = "BR" });
        provision.EnsureSuccessStatusCode();
        var tenant = await provision.Content.ReadFromJsonAsync<JsonElement>();
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        refresh.EnsureSuccessStatusCode();
        auth = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return (client, tenant.GetProperty("tenantId").GetGuid(), tenant.GetProperty("companyId").GetGuid(), email);
    }

    private static async Task SyncAsync((HttpClient Client, Guid TenantId, Guid CompanyId, string Email) account, int count)
    {
        using var register = await account.Client.PostAsJsonAsync("/api/v1/connectors", new { account.CompanyId, name = "Usage", baseUrl = "http://localhost:5080/api/v1/sample/customers", sourceEntity = "customers" });
        register.EnsureSuccessStatusCode();
        var instance = await register.Content.ReadFromJsonAsync<JsonElement>();
        for (var i = 0; i < count; i++)
        {
            using var response = await account.Client.PostAsync($"/api/v1/connectors/{instance.GetProperty("id").GetGuid()}/sync", null);
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
    }

    private static string UsageUrl(Guid tenantId, DateTimeOffset from, DateTimeOffset to) =>
        $"/api/v1/admin/tenants/{tenantId}/usage?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

    private sealed class NoOpPublisher : IConnectorSyncPublisher
    {
        public Task PublishAsync(SyncRequestedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
