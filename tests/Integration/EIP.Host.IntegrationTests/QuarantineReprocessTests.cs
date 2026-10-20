using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Canonical.Domain;
using EIP.Data.Canonical.Infrastructure;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

[Collection(HostApiCollection.Name)]
public sealed class QuarantineReprocessTests(HostApiFixture fixture)
{
    [Fact]
    public async Task Reprocess_IsFullTenantScopedIdempotentAndRespectsQuota()
    {
        var publisher = new RecordingPublisher();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new[] { "TenantDb", "IdentityDb", "ConnectorDb", "CanonicalDb", "WarehouseDb", "DashboardDb", "UsageDb" }
                    .Select(name => new KeyValuePair<string, string?>($"ConnectionStrings:{name}", fixture.ConnectionString))
                    .Append(new("ConnectionStrings:Redis", fixture.RedisConnectionString))));
            builder.ConfigureTestServices(services => services.AddSingleton<IConnectorSyncPublisher>(publisher));
        });
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        var tenant = await ProvisionAsync(a);
        await ProvisionAsync(b);
        using var registered = await a.PostAsJsonAsync("/api/v1/connectors", new
        {
            companyId = tenant.CompanyId,
            name = "Quarantine",
            sourceEntity = "customers",
            baseUrl = "http://localhost:5080/api/v1/sample/customers",
        });
        registered.EnsureSuccessStatusCode();
        var connector = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var first = CanonicalQuarantineEntry.Create(tenant.TenantId, connector, Guid.NewGuid(), "customers", "synthetic://quarantine", "test", "TEST", "Synthetic");
        var second = CanonicalQuarantineEntry.Create(tenant.TenantId, connector, Guid.NewGuid(), "customers", "synthetic://quarantine", "test", "TEST", "Synthetic");
        var accessor = factory.Services.GetRequiredService<ITenantContextAccessor>();
        var dbFactory = factory.Services.GetRequiredService<IDbContextFactory<CanonicalDbContext>>();
        accessor.Current = new TenantContext(tenant.TenantId);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            db.QuarantineEntries.AddRange(first, second);
            await db.SaveChangesAsync();
        }
        finally { accessor.Current = null; }

        using var foreign = await b.PostAsync($"/api/v1/connectors/quarantine/{first.Id}/reprocess", null);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var accepted = await a.PostAsync($"/api/v1/connectors/quarantine/{first.Id}/reprocess", null);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        publisher.Messages.Should().ContainSingle().Which.ReprocessFromUtc.Should().Be(SyncRequestedMessage.FullReprocessFromUtc);
        using var duplicate = await a.PostAsync($"/api/v1/connectors/quarantine/{first.Id}/reprocess", null);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var quota = await a.PostAsync($"/api/v1/connectors/quarantine/{second.Id}/reprocess", null);
        quota.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        quota.Headers.RetryAfter!.Delta.Should().BePositive();
        publisher.Messages.Should().ContainSingle();
        accessor.Current = new TenantContext(tenant.TenantId);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            (await db.QuarantineEntries.SingleAsync(e => e.Id == first.Id)).ResolvedAt.Should().NotBeNull();
            (await db.QuarantineEntries.SingleAsync(e => e.Id == second.Id)).ResolvedAt.Should().BeNull();
        }
        finally { accessor.Current = null; }
    }

    private static async Task<(Guid TenantId, Guid CompanyId)> ProvisionAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = $"quarantine-{Guid.NewGuid():N}@test.local", password = "QuarantineTest!123", displayName = "Quarantine" });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        using var provision = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Quarantine", slug = $"quarantine-{Guid.NewGuid():N}", countryCode = "BR" });
        provision.EnsureSuccessStatusCode();
        var tenant = await provision.Content.ReadFromJsonAsync<JsonElement>();
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        refresh.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await refresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        return (tenant.GetProperty("tenantId").GetGuid(), tenant.GetProperty("companyId").GetGuid());
    }

    private sealed class RecordingPublisher : IConnectorSyncPublisher
    {
        public List<SyncRequestedMessage> Messages { get; } = [];
        public Task PublishAsync(SyncRequestedMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
