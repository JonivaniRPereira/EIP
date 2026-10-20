using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Intelligence.AI.Application;
using EIP.Intelligence.AI.Infrastructure;
using EIP.Platform.Identity.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

[Collection(HostApiCollection.Name)]
public sealed class AiCredentialTests(HostApiFixture fixture)
{
    private const string CredentialUrl = "/api/v1/ai/settings/profiles/profile/credential";

    [Fact]
    public async Task CredentialEndpointsRequireAdministrativePermissionAndSelectedTenant()
    {
        using var client = fixture.CreateClient();
        (await client.GetAsync("/api/v1/ai/settings/credentials")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync(CredentialUrl, Secret(1))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync(CredentialUrl + "?revision=1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var auth = await AiToolsTests.RegisterAsync(client);
        (await client.GetAsync("/api/v1/ai/settings/credentials")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var tenant = await AiToolsTests.SelectNewTenantAsync(client, auth);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter);
        var token = fixture.Services.GetRequiredService<IJwtTokenGenerator>()
            .GenerateAccessToken(Guid.Parse(original.Subject), "credential@test.local", tenant, ["analytics.query"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        (await client.GetAsync("/api/v1/ai/settings/credentials")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync(CredentialUrl, Secret(1))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync(CredentialUrl + "?revision=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StoredValueIsEncryptedPurposeBoundAndNeverReturnedByApi()
    {
        using var client = fixture.CreateClient();
        var tenant = await ProvisionAsync(client);
        var secret = Secret(1);
        using var response = await client.PutAsJsonAsync(CredentialUrl, secret);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain(secret.Secret);
        var status = JsonSerializer.Deserialize<AiCredentialStatus>(json, JsonSerializerOptions.Web)!;
        status.Revision.Should().Be(2);
        status.ConfiguredProfileIds.Should().ContainSingle().Which.Should().Be("profile");
        var accessor = fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var previous = accessor.Current;
        accessor.Current = new TenantContext(tenant);
        try
        {
            await using var db = await fixture.Services.GetRequiredService<IDbContextFactory<AiDbContext>>().CreateDbContextAsync();
            var stored = (await db.Settings.SingleAsync(row => row.TenantId == tenant)).ProtectedCredentials!;
            stored.Should().NotContain(secret.Secret);
            var protectedValue = JsonDocument.Parse(stored).RootElement.GetProperty("profile").GetProperty("Value").GetString()!;
            var protector = fixture.Services.GetRequiredService<IAiCredentialProtector>();
            protector.Unprotect(tenant, "profile", "openai", protectedValue).Should().Be(secret.Secret);
            Action wrongTenant = () => protector.Unprotect(Guid.NewGuid(), "profile", "openai", protectedValue);
            Action wrongProfile = () => protector.Unprotect(tenant, "other", "openai", protectedValue);
            Action wrongProvider = () => protector.Unprotect(tenant, "profile", "anthropic", protectedValue);
            wrongTenant.Should().Throw<CryptographicException>();
            wrongProfile.Should().Throw<CryptographicException>();
            wrongProvider.Should().Throw<CryptographicException>();
            foreach (var path in new[] { "/api/v1/ai/settings", "/api/v1/ai/settings/credentials", "/api/v1/ai/providers" })
            {
                var body = await client.GetStringAsync(path);
                body.Should().NotContain(secret.Secret).And.NotContain(protectedValue);
            }
        }
        finally { accessor.Current = previous; }
        secret.ToString().Should().NotContain(secret.Secret);
    }

    [Fact]
    public async Task TenantsWithSameProfileIdHaveIndependentCredentials()
    {
        using var a = fixture.CreateClient();
        using var b = fixture.CreateClient();
        await ProvisionAsync(a);
        await ProvisionAsync(b);
        using var saved = await a.PutAsJsonAsync(CredentialUrl, Secret(1));
        saved.EnsureSuccessStatusCode();
        (await b.GetFromJsonAsync<AiCredentialStatus>("/api/v1/ai/settings/credentials"))!.ConfiguredProfileIds.Should().BeEmpty();
        using var mismatched = await b.PutAsJsonAsync(CredentialUrl, Secret(2));
        mismatched.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var own = await b.PutAsJsonAsync(CredentialUrl, Secret(1));
        own.EnsureSuccessStatusCode();
        (await a.GetFromJsonAsync<AiCredentialStatus>("/api/v1/ai/settings/credentials"))!.Revision.Should().Be(2);
    }

    [Fact]
    public async Task ReplacementAndRemovalShareTheSettingsRevisionAndDoNotEnableModels()
    {
        using var client = fixture.CreateClient();
        await ProvisionAsync(client);
        (await client.PutAsJsonAsync(CredentialUrl, Secret(1))).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(CredentialUrl, Secret(2))).EnsureSuccessStatusCode();
        (await client.DeleteAsync(CredentialUrl + "?revision=2")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var removed = await client.DeleteAsync(CredentialUrl + "?revision=3");
        removed.EnsureSuccessStatusCode();
        var status = (await removed.Content.ReadFromJsonAsync<AiCredentialStatus>())!;
        status.Revision.Should().Be(4);
        status.ConfiguredProfileIds.Should().BeEmpty();
        (await client.GetFromJsonAsync<AiModelStatus>("/api/v1/ai/status"))!.Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("rename")]
    [InlineData("provider")]
    [InlineData("remove")]
    public async Task ProfileChangesDiscardThePreviousCredentialAtomically(string change)
    {
        using var client = fixture.CreateClient();
        await ProvisionAsync(client);
        (await client.PutAsJsonAsync(CredentialUrl, Secret(1))).EnsureSuccessStatusCode();
        var profiles = change == "remove" ? Array.Empty<AiProviderProfile>()
            : new[] { new AiProviderProfile(change == "rename" ? "renamed" : "profile", change == "provider" ? "anthropic" : "openai", "demo-model", 30, 1024) };
        (await client.PutAsJsonAsync("/api/v1/ai/settings", new SaveAiSettingsRequest(2, null, profiles))).EnsureSuccessStatusCode();
        var status = (await client.GetFromJsonAsync<AiCredentialStatus>("/api/v1/ai/settings/credentials"))!;
        status.Revision.Should().Be(3);
        status.ConfiguredProfileIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains\nnewline")]
    [InlineData("too-long")]
    public async Task InvalidSecretsAreRejectedWithoutEchoOrPersistence(string value)
    {
        using var client = fixture.CreateClient();
        await ProvisionAsync(client);
        var secret = value == "too-long" ? new string('x', 2049) : value;
        using var response = await client.PutAsJsonAsync(CredentialUrl, new SaveAiCredentialRequest { Revision = 1, Secret = secret });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        if (secret.Length > 0)
        {
            (await response.Content.ReadAsStringAsync()).Should().NotContain(secret);
        }

        (await client.GetFromJsonAsync<AiTenantSettings>("/api/v1/ai/settings"))!.Revision.Should().Be(1);
    }

    [Fact]
    public async Task ExtraContextFieldsAreRejected()
    {
        using var client = fixture.CreateClient();
        await ProvisionAsync(client);
        using var body = new StringContent("{\"revision\":1,\"secret\":\"synthetic-value\",\"tenantId\":\"11111111-1111-1111-1111-111111111111\"}", Encoding.UTF8, "application/json");
        using var response = await client.PutAsync(CredentialUrl, body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<AiCredentialStatus>("/api/v1/ai/settings/credentials"))!.ConfiguredProfileIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrFailedProtectionNeverFallsBackToPlaintext(bool fails)
    {
        using var client = fixture.CreateClient();
        var tenant = await ProvisionAsync(client);
        IAiCredentialProtector protector = fails ? new FailingProtector() : new UnavailableAiCredentialProtector();
        var store = new AiCredentialStore(fixture.Services.GetRequiredService<IDbContextFactory<AiDbContext>>(), protector);
        var accessor = fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var previous = accessor.Current;
        accessor.Current = new TenantContext(tenant);
        try
        {
            (await store.SaveAsync(tenant, Guid.NewGuid(), "profile", Secret(1), CancellationToken.None)).Code.Should().Be("protection_unavailable");
            await using var db = await fixture.Services.GetRequiredService<IDbContextFactory<AiDbContext>>().CreateDbContextAsync();
            var record = await db.Settings.SingleAsync(row => row.TenantId == tenant);
            record.Revision.Should().Be(1);
            record.ProtectedCredentials.Should().BeNull();
        }
        finally { accessor.Current = previous; }
    }

    [Fact]
    public async Task CredentialAndProviderChangesCannotRaceIntoTheWrongBinding()
    {
        using var client = fixture.CreateClient();
        await ProvisionAsync(client);
        var responses = await Task.WhenAll(client.PutAsJsonAsync(CredentialUrl, Secret(1)),
            client.PutAsJsonAsync("/api/v1/ai/settings", new SaveAiSettingsRequest(1, null, [new("profile", "anthropic", "demo-model", 30, 1024)])));
        try
        {
            responses.Select(response => response.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            var settings = (await client.GetFromJsonAsync<AiTenantSettings>("/api/v1/ai/settings"))!;
            var credentials = (await client.GetFromJsonAsync<AiCredentialStatus>("/api/v1/ai/settings/credentials"))!;
            settings.Revision.Should().Be(2);
            credentials.ConfiguredProfileIds.Count.Should().Be(settings.Profiles[0].Provider == "openai" ? 1 : 0);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static SaveAiCredentialRequest Secret(long revision) => new() { Revision = revision, Secret = "synthetic-credential-" + Guid.NewGuid().ToString("N") };
    private static async Task<Guid> ProvisionAsync(HttpClient client)
    {
        var tenant = await AiToolsTests.SelectNewTenantAsync(client, await AiToolsTests.RegisterAsync(client));
        (await client.PutAsJsonAsync("/api/v1/ai/settings", new SaveAiSettingsRequest(0, null, [new("profile", "openai", "demo-model", 30, 1024)]))).EnsureSuccessStatusCode();
        return tenant;
    }

    private sealed class FailingProtector : IAiCredentialProtector
    {
        public bool Available => true;
        public string Protect(Guid tenantId, string profileId, string provider, string secret) => throw new CryptographicException("Synthetic protection failure");
        public string Unprotect(Guid tenantId, string profileId, string provider, string protectedValue) => throw new NotSupportedException();
    }
}
