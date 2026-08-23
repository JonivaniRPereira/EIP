using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E1.5 (`docs/roadmap/fase-2-backlog.md`): consulta que excede o
/// guardrail de período máximo (<c>Analytics:MaxPeriodDays</c>, default 400 dias) ou o guardrail de
/// linhas de resposta (<c>Analytics:MaxRows</c>, default 5000) retorna 400 com mensagem explicando
/// qual limite foi violado — nunca executa a consulta contra o Warehouse (não há necessidade de
/// semear dado nenhum: ambos os guardrails são checados antes de qualquer leitura). O guardrail de
/// timeout tem sua própria suíte (<c>AnalyticsQueryTimeoutTests</c>), já que precisa de uma
/// configuração diferente do resto da coleção.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class AnalyticsGuardrailTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _email = $"analytics-guardrail-{Guid.NewGuid():N}@test.com";

    private Guid _tenantId;
    private string _token = null!;

    public AnalyticsGuardrailTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var userId = await RegisterAsync(_email);
        _tenantId = await ProvisionTenantAndMembershipAsync(userId);
        _token = await LoginAsync(_email);
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
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

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<EIP.Platform.Identity.Infrastructure.AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _email).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Query_DateFilterExceedingMaxPeriod_ReturnsBadRequestExplainingTheLimit()
    {
        using var response = await SendQueryAsync(new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
            filters = new[] { new { field = "date", @operator = "between", values = new[] { "2020-01-01", "2026-01-01" } } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().Contain("período máximo");
    }

    [Fact]
    public async Task Query_LimitExceedingMaxRows_ReturnsBadRequestExplainingTheLimit()
    {
        using var response = await SendQueryAsync(new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
            limit = 10_000,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().Contain("linhas de resposta");
    }

    private Task<HttpResponseMessage> SendQueryAsync(object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/query")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return _client.SendAsync(request);
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
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenant = Tenant.Create("Analytics Guardrail Tenant (E1.5)", $"e1-5-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

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
