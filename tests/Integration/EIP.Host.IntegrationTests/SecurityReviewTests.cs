using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using EIP.Host;
using EIP.Shared.Contracts.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using EIP.Data.Semantic.Application;
using EIP.Platform.Identity.Domain;
using EIP.Platform.Identity.Application.Abstractions;
using EIP.Shared.Contracts.Analytics;
using EIP.BuildingBlocks.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

[Collection(HostApiCollection.Name)]
public sealed class SecurityReviewTests(HostApiFixture fixture)
{
    [Fact]
    public async Task UnexpectedError_DoesNotReturnInternalExceptionDetail()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
            ]));
            builder.ConfigureTestServices(services => services.AddScoped<IAnalyticsQueryService>(_ =>
                throw new InvalidOperationException("INTERNAL_SECRET_TEST_MARKER")));
        });
        using var client = factory.CreateClient();
        await ProvisionAsync(client);
        using var response = await client.PostAsJsonAsync("/api/v1/analytics/query", new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
        });
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("INTERNAL_SECRET_TEST_MARKER");
        body.Should().NotContain("System.InvalidOperationException");
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_AndIsIdempotent()
    {
        using var client = fixture.CreateClient();
        var auth = await RegisterAsync(client);
        var body = new { refreshToken = auth.GetProperty("refreshToken").GetString() };
        using var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", body);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", body);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var repeated = await client.PostAsJsonAsync("/api/v1/auth/logout", body);
        repeated.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task SignedToken_CannotSelectEmptyOrSystemTenant(string tenant)
    {
        using var client = fixture.CreateClient();
        var generator = fixture.Services.GetRequiredService<IJwtTokenGenerator>();
        var token = generator.GenerateAccessToken(Guid.NewGuid(), "test@test.local", Guid.Parse(tenant), [AnalyticsPermissions.AnalyticsQuery]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        using var response = await client.GetAsync("/api/v1/analytics/datasets");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlatformPermissionOutsideDevelopment_AlsoRequiresMfa()
    {
        using var client = fixture.CreateClient();
        var email = $"mfa-{Guid.NewGuid():N}@test.local";
        await RegisterAsync(client, email);
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        (await users.AddClaimAsync(user, new Claim(PlatformPermissions.ClaimType, PlatformPermissions.TenantUsageRead))).Succeeded.Should().BeTrue();
        var handler = new PlatformUsageAuthorizationHandler(users, new ProductionEnvironment());
        var requirement = new PlatformUsageRequirement();
        var claims = new List<Claim> { new("sub", user.Id.ToString()) };
        var withoutMfa = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);
        await handler.HandleAsync(withoutMfa);
        withoutMfa.HasSucceeded.Should().BeFalse();
        claims.Add(new Claim("amr", "mfa"));
        var withMfa = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);
        await handler.HandleAsync(withMfa);
        withMfa.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_ConcurrentReplay_OnlyOneRequestWins_AndReplacementWorks()
    {
        using var client = fixture.CreateClient();
        var auth = await RegisterAsync(client);
        var body = new { refreshToken = auth.GetProperty("refreshToken").GetString() };
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/v1/auth/refresh", body)));
        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(7);
            var replacement = await responses.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<JsonElement>();
            using var next = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = replacement.GetProperty("refreshToken").GetString() });
            next.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            foreach (var response in responses) { response.Dispose(); }
        }
    }

    [Fact]
    public async Task LockedUser_CannotRefreshOrSelectTenant_AndLoginIsGeneric()
    {
        using var client = fixture.CreateClient();
        var email = $"security-{Guid.NewGuid():N}@test.local";
        var auth = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        using var provision = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Security", slug = $"security-{Guid.NewGuid():N}", countryCode = "BR" });
        provision.EnsureSuccessStatusCode();
        var tenant = await provision.Content.ReadFromJsonAsync<JsonElement>();
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        (await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded.Should().BeTrue();
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var select = await client.PostAsJsonAsync("/api/v1/auth/select-tenant", new { tenantId = tenant.GetProperty("tenantId").GetGuid() });
        select.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SecurityTest!123" });
        using var unknown = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = $"missing-{Guid.NewGuid():N}@test.local", password = "SecurityTest!123" });
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var lockedBody = await locked.Content.ReadFromJsonAsync<JsonElement>();
        var unknownBody = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        lockedBody.GetProperty("detail").GetString().Should().Be(unknownBody.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Connector_RejectsOtherCompanyAndUnapprovedDestination()
    {
        using var a = fixture.CreateClient();
        using var b = fixture.CreateClient();
        var tenantA = await ProvisionAsync(a);
        var tenantB = await ProvisionAsync(b);
        var allowedUrl = "http://localhost:5080/api/v1/sample/customers";
        using var crossed = await a.PostAsJsonAsync("/api/v1/connectors", new { companyId = tenantB.GetProperty("companyId").GetGuid(), name = "Crossed", baseUrl = allowedUrl, sourceEntity = "customers" });
        crossed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreach (var url in new[] { "http://169.254.169.254/latest/meta-data", "file:///etc/passwd", "http://localhost:5080/metrics", "http://user:secret@localhost:5080/api/v1/sample/customers" })
        {
            using var denied = await a.PostAsJsonAsync("/api/v1/connectors", new { companyId = tenantA.GetProperty("companyId").GetGuid(), name = "Denied", baseUrl = url, sourceEntity = "customers" });
            denied.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        using var valid = await a.PostAsJsonAsync("/api/v1/connectors", new { companyId = tenantA.GetProperty("companyId").GetGuid(), name = "Allowed", baseUrl = allowedUrl, sourceEntity = "customers" });
        valid.StatusCode.Should().Be(HttpStatusCode.Created);
        using var unsafeEntity = await a.PostAsJsonAsync("/api/v1/connectors", new { companyId = tenantA.GetProperty("companyId").GetGuid(), name = "Unsafe", baseUrl = allowedUrl, sourceEntity = "../../other-tenant" });
        unsafeEntity.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AuthValidationAndHeaders_DoNotExposeOrCacheCredentials()
    {
        using var client = fixture.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalid.Headers.CacheControl!.NoStore.Should().BeTrue();
        invalid.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        invalid.Headers.GetValues("Referrer-Policy").Should().ContainSingle("no-referrer");
        using var malformed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = new string('x', 300) });
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.invalid.invalid");
        using var denied = await client.GetAsync("/api/v1/analytics/datasets");
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<JsonElement> RegisterAsync(HttpClient client, string? email = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = email ?? $"security-{Guid.NewGuid():N}@test.local", password = "SecurityTest!123", displayName = "Security" });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ProvisionAsync(HttpClient client)
    {
        var auth = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        using var provision = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Security", slug = $"security-{Guid.NewGuid():N}", countryCode = "BR" });
        provision.EnsureSuccessStatusCode();
        var tenant = await provision.Content.ReadFromJsonAsync<JsonElement>();
        using var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        refresh.EnsureSuccessStatusCode();
        var refreshed = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.GetProperty("accessToken").GetString());
        return tenant;
    }

    private sealed class ProductionEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "EIP.Host";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
