using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using EIP.Data.Connector.Domain;
using EIP.Data.Connector.Infrastructure;
using EIP.Host;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EIP.Host.IntegrationTests;

[Collection(HostApiCollection.Name)]
public sealed class ConnectorSyncQuotaTests
{
    private readonly HostApiFixture _fixture;

    public ConnectorSyncQuotaTests(HostApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RequestSync_ConcurrentRequests_RejectsExcessWithoutSavingOrPublishing()
    {
        var publisher = new RecordingPublisher();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("ConnectionStrings:TenantDb", _fixture.ConnectionString),
                new("ConnectionStrings:IdentityDb", _fixture.ConnectionString),
                new("ConnectionStrings:ConnectorDb", _fixture.ConnectionString),
                new("ConnectionStrings:CanonicalDb", _fixture.ConnectionString),
                new("ConnectionStrings:WarehouseDb", _fixture.ConnectionString),
                new("ConnectionStrings:DashboardDb", _fixture.ConnectionString),
                new("ConnectionStrings:Redis", _fixture.RedisConnectionString),
                new("Connector:SyncQuota:PermitLimit", "2"),
                new("Connector:SyncQuota:WindowSeconds", "60"),
            ]));
            builder.ConfigureTestServices(services => services.AddSingleton<IConnectorSyncPublisher>(publisher));
        });
        using var client = factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"quota-{Guid.NewGuid():N}@test.local",
            password = "QuotaTest!123",
            displayName = "Quota Test",
        });
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        using var provision = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Quota Test",
            slug = $"quota-{Guid.NewGuid():N}",
            countryCode = "BR",
        });
        provision.EnsureSuccessStatusCode();
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        refresh.EnsureSuccessStatusCode();
        auth = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        auth.GetProperty("requiresTenantSelection").GetBoolean().Should().BeFalse();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());

        var tenant = await provision.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenant.GetProperty("tenantId").GetGuid();
        var accessor = factory.Services.GetRequiredService<ITenantContextAccessor>();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<ConnectorDbContext>>();
        accessor.Current = new TenantContext(tenantId);
        var companyId = tenant.GetProperty("companyId").GetGuid();
        var instance = ConnectorInstance.Create(tenantId, companyId, "Quota", "http://localhost/sample", "customers");
        var otherInstance = ConnectorInstance.Create(tenantId, companyId, "Other", "http://localhost/sample", "customers");
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.ConnectorInstances.AddRange(instance, otherInstance);
            await db.SaveChangesAsync();
        }
        accessor.Current = null;

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => client.PostAsync($"/api/v1/connectors/{instance.Id}/sync", null)));
        try
        {
            var diagnostics = string.Join("; ", await Task.WhenAll(responses.Select(async r => $"{r.StatusCode}: {await r.Content.ReadAsStringAsync()}")));
            responses.Count(r => r.StatusCode == HttpStatusCode.Accepted).Should().Be(2, diagnostics);
            var rejected = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).ToArray();
            rejected.Should().HaveCount(6);
            foreach (var response in rejected)
            {
                response.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
                response.Headers.RetryAfter.Delta.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
                problem.GetProperty("status").GetInt32().Should().Be(429);
            }

            publisher.Count.Should().Be(2);
            accessor.Current = new TenantContext(tenantId);
            await using var db = await dbFactory.CreateDbContextAsync();
            (await db.SyncRuns.CountAsync(r => r.ConnectorInstanceId == instance.Id)).Should().Be(2);
            accessor.Current = null;

            using var independent = await client.PostAsync($"/api/v1/connectors/{otherInstance.Id}/sync?reprocessFrom=2026-01-01T00:00:00Z", null);
            independent.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
        finally
        {
            accessor.Current = null;
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Quota_IsSharedAcrossClients_IsolatesTenants_AndExpires()
    {
        using var redisA = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        using var redisB = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        var options = Options.Create(new ConnectorSyncQuotaOptions { PermitLimit = 1, WindowSeconds = 1 });
        var first = new RedisConnectorSyncQuota(redisA, options);
        var second = new RedisConnectorSyncQuota(redisB, options);
        var tenantId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        (await first.TryAcquireAsync(tenantId, connectorId, CancellationToken.None)).Should().BeNull();
        (await second.TryAcquireAsync(tenantId, connectorId, CancellationToken.None)).Should().BePositive();
        (await second.TryAcquireAsync(Guid.NewGuid(), connectorId, CancellationToken.None)).Should().BeNull();
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        (await second.TryAcquireAsync(tenantId, connectorId, CancellationToken.None)).Should().BeNull();
    }

    private sealed class RecordingPublisher : IConnectorSyncPublisher
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public Task PublishAsync(SyncRequestedMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            return Task.CompletedTask;
        }
    }
}
