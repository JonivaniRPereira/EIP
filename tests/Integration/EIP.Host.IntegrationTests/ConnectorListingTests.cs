using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Domain;
using EIP.Data.Connector.Infrastructure;
using EIP.Platform.Identity.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

[Collection(HostApiCollection.Name)]
public sealed class ConnectorListingTests(HostApiFixture fixture)
{
    [Fact]
    public async Task Configuration_IsTenantScopedValidatedAndPreservedByWatermarkWrites()
    {
        using var a = fixture.CreateClient();
        using var b = fixture.CreateClient();
        var org = await ProvisionAsync(a);
        await ProvisionAsync(b);
        var id = await RegisterAsync(a, org.CompanyId);
        var url = $"/api/v1/connectors/{id}/configuration";
        var body = new { name = "Updated", baseUrl = "http://localhost:5080/api/v1/sample/customers" };
        using var foreign = await b.PutAsJsonAsync(url, body);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var blocked = await a.PutAsJsonAsync(url, new { name = "Updated", baseUrl = "http://169.254.169.254/latest/meta-data" });
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var accessor = fixture.Services.GetRequiredService<ITenantContextAccessor>();
        using var scope = fixture.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EIP.Data.Connector.Application.Abstractions.IConnectorSyncStore>();
        accessor.Current = new TenantContext(org.TenantId);
        var stale = (await store.FindInstanceAsync(id, default))!;
        accessor.Current = null;
        using var saved = await a.PutAsJsonAsync(url, body);
        saved.EnsureSuccessStatusCode();
        accessor.Current = new TenantContext(org.TenantId);
        try
        {
            stale.AdvanceWatermark(DateTimeOffset.UtcNow.AddDays(-1));
            await store.SaveInstanceAsync(stale, default);
            var current = (await store.FindInstanceAsync(id, default))!;
            current.Name.Should().Be("Updated");
            current.CompanyId.Should().Be(org.CompanyId);
            current.SourceEntity.Should().Be("customers");
            current.LastWatermark.Should().Be(stale.LastWatermark);
        }
        finally { accessor.Current = null; }
        using var future = await a.PostAsync($"/api/v1/connectors/{id}/sync?reprocessFrom=2999-01-01T00:00:00Z", null);
        future.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(a.DefaultRequestHeaders.Authorization!.Parameter);
        var readOnly = fixture.Services.GetRequiredService<IJwtTokenGenerator>().GenerateAccessToken(Guid.Parse(token.Subject), "readonly@test.local", org.TenantId, ["connector.view"]);
        a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", readOnly.Value);
        using var denied = await a.PutAsJsonAsync(url, body);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task QuarantineCursor_FiltersBeforePagingAndHandlesEqualDates()
    {
        using var a = fixture.CreateClient();
        using var b = fixture.CreateClient();
        var org = await ProvisionAsync(a);
        await ProvisionAsync(b);
        var connector = await RegisterAsync(a, org.CompanyId);
        var entries = Enumerable.Range(0, 3).Select(_ => EIP.Data.Canonical.Domain.CanonicalQuarantineEntry.Create(org.TenantId, connector,
            Guid.NewGuid(), "customers", "synthetic://pagination", "pagination", "TEST", "Paging test")).ToArray();
        entries[0].MarkResolved(DateTimeOffset.UtcNow);
        var accessor = fixture.Services.GetRequiredService<ITenantContextAccessor>();
        accessor.Current = new TenantContext(org.TenantId);
        try
        {
            var factory = fixture.Services.GetRequiredService<IDbContextFactory<EIP.Data.Canonical.Infrastructure.CanonicalDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            db.QuarantineEntries.AddRange(entries);
            foreach (var entry in entries)
            {
                db.Entry(entry).Property(e => e.CreatedAt).CurrentValue = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            }

            await db.SaveChangesAsync();
        }
        finally { accessor.Current = null; }
        var url = $"/api/v1/connectors/quarantine?connectorInstanceId={connector}&limit=1&includeRequested=false";
        var first = (await a.GetFromJsonAsync<JsonElement[]>(url))!;
        first.Should().ContainSingle();
        var cursor = $"&before={Uri.EscapeDataString(first[0].GetProperty("createdAt").GetString()!)}&beforeId={first[0].GetProperty("id").GetGuid()}";
        var second = (await a.GetFromJsonAsync<JsonElement[]>(url + cursor))!;
        second.Should().ContainSingle();
        second[0].GetProperty("id").GetGuid().Should().NotBe(first[0].GetProperty("id").GetGuid());
        new[] { first[0].GetProperty("id").GetGuid(), second[0].GetProperty("id").GetGuid() }.Should().BeEquivalentTo(entries.Skip(1).Select(e => e.Id));
        (await b.GetFromJsonAsync<JsonElement[]>(url)).Should().BeEmpty();
        (await a.GetFromJsonAsync<JsonElement[]>($"/api/v1/connectors/quarantine?connectorInstanceId={connector}&limit=10&includeRequested=true"))!.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListsAndHistory_AreTenantScopedAndBounded()
    {
        using var a = fixture.CreateClient();
        using var b = fixture.CreateClient();
        var orgA = await ProvisionAsync(a);
        var orgB = await ProvisionAsync(b);
        var instanceA = await RegisterAsync(a, orgA.CompanyId);
        var instanceB = await RegisterAsync(b, orgB.CompanyId);
        var accessor = fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var factory = fixture.Services.GetRequiredService<IDbContextFactory<ConnectorDbContext>>();
        accessor.Current = new TenantContext(orgA.TenantId);
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            var older = SyncRun.CreatePending(orgA.TenantId, instanceA, "older");
            var newer = SyncRun.CreatePending(orgA.TenantId, instanceA, "newer");
            db.SyncRuns.AddRange(older, newer);
            var sameDate = DateTimeOffset.UtcNow.AddDays(-1);
            db.Entry(older).Property(r => r.CreatedAt).CurrentValue = sameDate;
            db.Entry(newer).Property(r => r.CreatedAt).CurrentValue = sameDate;
            await db.SaveChangesAsync();
        }
        finally { accessor.Current = null; }

        var list = await a.GetFromJsonAsync<JsonElement[]>("/api/v1/connectors");
        list!.Should().ContainSingle();
        list![0].GetProperty("id").GetGuid().Should().Be(instanceA);
        var companies = await a.GetFromJsonAsync<JsonElement[]>($"/api/v1/tenants/{orgA.TenantId}/companies");
        companies!.Should().ContainSingle();
        companies![0].GetProperty("id").GetGuid().Should().Be(orgA.CompanyId);
        using var foreignCompany = await a.GetAsync($"/api/v1/tenants/{orgB.TenantId}/companies");
        foreignCompany.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var foreignRuns = await a.GetAsync($"/api/v1/connectors/{instanceB}/sync-runs");
        foreignRuns.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var runs = await a.GetFromJsonAsync<JsonElement[]>($"/api/v1/connectors/{instanceA}/sync-runs?limit=1");
        runs!.Should().ContainSingle();
        runs![0].GetProperty("connectorInstanceId").GetGuid().Should().Be(instanceA);
        var cursor = $"before={Uri.EscapeDataString(runs[0].GetProperty("createdAt").GetString()!)}&beforeId={runs[0].GetProperty("id").GetGuid()}";
        accessor.Current = new TenantContext(orgA.TenantId);
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            db.SyncRuns.Add(SyncRun.CreatePending(orgA.TenantId, instanceA, "arrived-between-pages"));
            await db.SaveChangesAsync();
        }
        finally { accessor.Current = null; }
        var next = await a.GetFromJsonAsync<JsonElement[]>($"/api/v1/connectors/{instanceA}/sync-runs?limit=1&{cursor}");
        next!.Should().ContainSingle();
        next![0].GetProperty("id").GetGuid().Should().NotBe(runs[0].GetProperty("id").GetGuid());
        next[0].GetProperty("createdAt").GetDateTimeOffset().Should().Be(runs[0].GetProperty("createdAt").GetDateTimeOffset());
        using var incompleteCursor = await a.GetAsync($"/api/v1/connectors/{instanceA}/sync-runs?beforeId={instanceA}");
        incompleteCursor.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var invalid = await a.GetAsync($"/api/v1/connectors/{instanceA}/sync-runs?limit=101");
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lists_RequireAuthenticationAndAppropriatePermissions()
    {
        using var client = fixture.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/connectors");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var org = await ProvisionAsync(client);
        var source = new JwtSecurityTokenHandler().ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter);
        var generator = fixture.Services.GetRequiredService<IJwtTokenGenerator>();
        var readOnly = generator.GenerateAccessToken(Guid.Parse(source.Subject), "listing@test.local", org.TenantId, ["connector.view"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", readOnly.Value);
        using var readable = await client.GetAsync("/api/v1/connectors");
        readable.StatusCode.Should().Be(HttpStatusCode.OK);
        using var noCompany = await client.GetAsync($"/api/v1/tenants/{org.TenantId}/companies");
        noCompany.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var noWrite = await client.PostAsJsonAsync("/api/v1/connectors", new { companyId = org.CompanyId, name = "Denied", baseUrl = "http://localhost:5080/api/v1/sample/customers", sourceEntity = "customers" });
        noWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var empty = generator.GenerateAccessToken(Guid.Parse(source.Subject), "listing@test.local", org.TenantId, []);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empty.Value);
        using var denied = await client.GetAsync("/api/v1/connectors");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<(Guid TenantId, Guid CompanyId)> ProvisionAsync(HttpClient client)
    {
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = $"listing-{Guid.NewGuid():N}@test.local", password = "ListingTest!123", displayName = "Listing" });
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        using var provision = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Listing", slug = $"listing-{Guid.NewGuid():N}", countryCode = "BR" });
        provision.EnsureSuccessStatusCode();
        var org = await provision.Content.ReadFromJsonAsync<JsonElement>();
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        refresh.EnsureSuccessStatusCode();
        var current = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.GetProperty("accessToken").GetString());
        return (org.GetProperty("tenantId").GetGuid(), org.GetProperty("companyId").GetGuid());
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, Guid companyId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/connectors", new { companyId, name = "Listing", baseUrl = "http://localhost:5080/api/v1/sample/customers", sourceEntity = "customers" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
