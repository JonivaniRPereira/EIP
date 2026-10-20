using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Intelligence.AI.Application;
using EIP.Intelligence.AI.Infrastructure;
using EIP.Platform.Identity.Application.Abstractions;
using EIP.Shared.Contracts.AI;
using EIP.Shared.Contracts.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

[Collection(HostApiCollection.Name)]
public sealed class AiSettingsTests(HostApiFixture fixture)
{
    [Fact]
    public async Task AdministrationRequiresAuthenticationSelectedTenantAndDedicatedPermission()
    {
        using var client = fixture.CreateClient();
        (await client.GetAsync("/api/v1/ai/settings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync("/api/v1/ai/settings", Request(0))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var auth = await AiToolsTests.RegisterAsync(client);
        (await client.GetAsync("/api/v1/ai/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var tenant = await AiToolsTests.SelectNewTenantAsync(client, auth);
        var source = new JwtSecurityTokenHandler().ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter);
        var generator = fixture.Services.GetRequiredService<IJwtTokenGenerator>();
        var token = generator.GenerateAccessToken(Guid.Parse(source.Subject), "ai-settings@test.local", tenant, ["analytics.query"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        (await client.GetAsync("/api/v1/ai/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync("/api/v1/ai/settings", Request(0))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/ai/settings/providers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        RolePermissions.Resolve("Member").Should().NotContain(AiPermissions.SettingsManage);
        RolePermissions.Resolve("Owner").Should().Contain(AiPermissions.SettingsManage);
        RolePermissions.Resolve("Admin").Should().Contain(AiPermissions.SettingsManage);
    }

    [Fact]
    public async Task SettingsPersistPerTenantExposeEffectiveCatalogAndRejectStaleRevisions()
    {
        using var a = fixture.CreateClient();
        using var b = fixture.CreateClient();
        await AiToolsTests.SelectNewTenantAsync(a, await AiToolsTests.RegisterAsync(a));
        await AiToolsTests.SelectNewTenantAsync(b, await AiToolsTests.RegisterAsync(b));
        var initial = await a.GetFromJsonAsync<AiTenantSettings>("/api/v1/ai/settings");
        initial!.Revision.Should().Be(0);
        using var save = await a.PutAsJsonAsync("/api/v1/ai/settings", Request(0));
        save.EnsureSuccessStatusCode();
        var saved = await save.Content.ReadFromJsonAsync<AiTenantSettings>();
        saved!.Revision.Should().Be(1);
        saved.InvocationEnabled.Should().BeFalse();
        saved.UpdatedAt.Should().NotBeNull();
        var reloaded = await a.GetFromJsonAsync<AiTenantSettings>("/api/v1/ai/settings");
        reloaded.Should().BeEquivalentTo(saved);
        var other = await b.GetFromJsonAsync<AiTenantSettings>("/api/v1/ai/settings");
        other!.Revision.Should().Be(0);
        other.ActiveProfileId.Should().BeNull();
        var catalog = await a.GetFromJsonAsync<AiProviderCatalog>("/api/v1/ai/providers");
        catalog!.ActiveProfileId.Should().Be("my-model");
        catalog.Profiles.Should().ContainSingle().Which.Model.Should().Be("demo-model");
        using var stale = await a.PutAsJsonAsync("/api/v1/ai/settings", Request(0));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var update = await a.PutAsJsonAsync("/api/v1/ai/settings", Request(1));
        update.EnsureSuccessStatusCode();
        (await update.Content.ReadFromJsonAsync<AiTenantSettings>())!.Revision.Should().Be(2);
        (await a.GetFromJsonAsync<AiModelStatus>("/api/v1/ai/status"))!.Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("{\"revision\":0,\"tenantId\":\"11111111-1111-1111-1111-111111111111\",\"profiles\":[]}")]
    [InlineData("{\"revision\":0,\"invocationEnabled\":true,\"profiles\":[]}")]
    [InlineData("{\"revision\":0,\"profiles\":[{\"id\":\"p\",\"provider\":\"openai\",\"model\":\"x\",\"timeoutSeconds\":30,\"maxOutputTokens\":1024,\"apiKey\":\"never-accepted\"}]}")]
    [InlineData("{\"revision\":0,\"profiles\":[null]}")]
    [InlineData("{\"revision\":0,\"activeProfileId\":\"missing\",\"profiles\":[]}")]
    [InlineData("{\"revision\":0,\"profiles\":[{\"id\":\"p\",\"provider\":\"unknown\",\"model\":\"x\",\"timeoutSeconds\":30,\"maxOutputTokens\":1024}]}")]
    public async Task InvalidSettingsCannotOverrideContextActivationOrCredentials(string json)
    {
        using var client = fixture.CreateClient();
        await AiToolsTests.SelectNewTenantAsync(client, await AiToolsTests.RegisterAsync(client));
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PutAsync("/api/v1/ai/settings", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<AiTenantSettings>("/api/v1/ai/settings"))!.Revision.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentFirstSavesCannotSilentlyOverwriteEachOther()
    {
        using var client = fixture.CreateClient();
        await AiToolsTests.SelectNewTenantAsync(client, await AiToolsTests.RegisterAsync(client));
        var responses = await Task.WhenAll(client.PutAsJsonAsync("/api/v1/ai/settings", Request(0)),
            client.PutAsJsonAsync("/api/v1/ai/settings", Request(0)));
        try
        {
            responses.Select(response => response.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            (await client.GetFromJsonAsync<AiTenantSettings>("/api/v1/ai/settings"))!.Revision.Should().Be(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task DatabaseRlsHidesOtherTenantsAndBlocksForeignWrites()
    {
        using var client = fixture.CreateClient();
        var tenantA = await AiToolsTests.SelectNewTenantAsync(client, await AiToolsTests.RegisterAsync(client));
        using var saved = await client.PutAsJsonAsync("/api/v1/ai/settings", Request(0));
        saved.EnsureSuccessStatusCode();
        var accessor = fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var previous = accessor.Current;
        accessor.Current = new TenantContext(Guid.NewGuid());
        try
        {
            await using var db = await fixture.Services.GetRequiredService<IDbContextFactory<AiDbContext>>().CreateDbContextAsync();
            (await db.Settings.CountAsync(row => row.TenantId == tenantA)).Should().Be(0);
            db.Settings.Add(new AiSettingsRecord { TenantId = Guid.NewGuid(), Revision = 1, Payload = "{}", UpdatedAt = DateTimeOffset.UtcNow });
            var write = async () => await db.SaveChangesAsync();
            await write.Should().ThrowAsync<DbUpdateException>();
        }
        finally
        {
            accessor.Current = previous;
        }
    }

    private static SaveAiSettingsRequest Request(long revision) => new(revision, "my-model",
        [new AiProviderProfile("my-model", "openai", "demo-model", 30, 1024)]);
}
