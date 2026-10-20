using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EIP.BuildingBlocks.Security;
using EIP.Data.Warehouse.Domain;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Platform.Administration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E1.2 (docs/roadmap/fase-2-backlog.md): <c>POST
/// /api/v1/analytics/query</c> nunca executa uma consulta com campo/métrica/dimensão inexistente
/// (retorna 400 com mensagem clara), uma consulta válida retorna dados + metadados completos
/// (dataset, versão semântica, executedAt, frescor, contagem de linhas), e o isolamento de tenant é
/// preservado mesmo com o novo módulo <c>Intelligence.Analytics</c> fora de <c>Data</c>/<c>Platform</c>.
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class AnalyticsQueryTests : IAsyncLifetime
{
    private readonly HostApiFixture _fixture;
    private readonly HttpClient _client;

    private const string Password = "SenhaForte!123";
    private readonly string _emailA = $"analytics-a-{Guid.NewGuid():N}@test.com";
    private readonly string _emailB = $"analytics-b-{Guid.NewGuid():N}@test.com";

    private Guid _tenantAId;
    private Guid _tenantBId;
    private string _tokenA = null!;
    private string _tokenB = null!;

    public AnalyticsQueryTests(HostApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var userAId = await RegisterAsync(_emailA);
        var userBId = await RegisterAsync(_emailB);

        (_tenantAId, _tenantBId) = await ProvisionTenantsAndMembershipsAsync(userAId, userBId);

        _tokenA = await LoginAsync(_emailA);
        _tokenB = await LoginAsync(_emailB);

        // Tenant A: 2 itens em meses diferentes (Jan/Fev) para provar o agrupamento por date.month,
        // mais um LoadBatch concluído para provar que "frescor" (dataFreshnessAt) é preenchido.
        await SeedFactSalesInvoiceItemAsync(_tenantAId, dateKey: 20260115, netAmount: 1000m);
        await SeedFactSalesInvoiceItemAsync(_tenantAId, dateKey: 20260215, netAmount: 2000m);
        await SeedSuccessfulLoadBatchAsync(_tenantAId);

        await SeedFactSalesInvoiceItemAsync(_tenantBId, dateKey: 20260115, netAmount: 99999m);
    }

    public async Task DisposeAsync()
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();

        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();
        tenantContextAccessor.Current = TenantContext.System;
        await using (var warehouseDb = await warehouseDbFactory.CreateDbContextAsync())
        {
            await warehouseDb.FactSalesInvoiceItems.Where(f => f.TenantId == _tenantAId || f.TenantId == _tenantBId).ExecuteDeleteAsync();
            await warehouseDb.DimCustomers.Where(c => c.TenantId == _tenantAId || c.TenantId == _tenantBId).ExecuteDeleteAsync();
            await warehouseDb.DimProducts.Where(p => p.TenantId == _tenantAId || p.TenantId == _tenantBId).ExecuteDeleteAsync();
            await warehouseDb.LoadBatches.Where(b => b.TenantId == _tenantAId || b.TenantId == _tenantBId).ExecuteDeleteAsync();
        }

        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
        await using (var tenantDb = await tenantDbFactory.CreateDbContextAsync())
        {
            await tenantDb.Memberships.Where(m => m.TenantId == _tenantAId || m.TenantId == _tenantBId).ExecuteDeleteAsync();
            await tenantDb.Tenants.Where(t => t.Id == _tenantAId || t.Id == _tenantBId).ExecuteDeleteAsync();
        }

        tenantContextAccessor.Current = null;

        await using var identityDb = _fixture.Services.GetRequiredService<IServiceScopeFactory>()
            .CreateScope().ServiceProvider.GetRequiredService<EIP.Platform.Identity.Infrastructure.AppIdentityDbContext>();
        await identityDb.Users.Where(u => u.Email == _emailA || u.Email == _emailB).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Query_WithUnknownMetric_ReturnsBadRequestAndNeverExecutes()
    {
        using var response = await SendQueryAsync(_tokenA, new
        {
            dataset = "sales",
            metrics = new[] { "notARealMetric" },
            dimensions = new[] { "date.month" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_WithUnknownDataset_ReturnsBadRequest()
    {
        using var response = await SendQueryAsync(_tokenA, new
        {
            dataset = "financial",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_WithMoreThanOneDimension_ReturnsBadRequest()
    {
        using var response = await SendQueryAsync(_tokenA, new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month", "customer" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_ValidRequest_ReturnsDataAndCompleteMetadata()
    {
        using var response = await SendQueryAsync(_tokenA, new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var metadata = body.GetProperty("metadata");
        metadata.GetProperty("dataset").GetString().Should().Be("sales");
        metadata.GetProperty("semanticVersion").GetString().Should().Be("1.0");
        metadata.GetProperty("rowCount").GetInt32().Should().Be(2);
        metadata.GetProperty("dataFreshnessAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        var data = body.GetProperty("data");
        data.GetArrayLength().Should().Be(2);
        var netRevenueTotal = data.EnumerateArray().Sum(row => row.GetProperty("netRevenue").GetDecimal());
        netRevenueTotal.Should().Be(3000m);
    }

    [Fact]
    public async Task Query_CrossTenant_NeverReturnsAnotherTenantsData()
    {
        using var response = await SendQueryAsync(_tokenB, new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var data = body.GetProperty("data");
        data.GetArrayLength().Should().Be(1);
        data[0].GetProperty("netRevenue").GetDecimal().Should().Be(99999m);
        body.GetProperty("metadata").GetProperty("dataFreshnessAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Query_ToolPreservesFiltersOrderingAndLimit()
    {
        using var response = await SendQueryAsync(_tokenA, new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
            filters = new[] { new { field = "date", @operator = "between", values = new[] { "2026-01-01", "2026-02-28" } } },
            orderBy = new[] { new { field = "netRevenue", direction = "desc" } },
            limit = 1,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetArrayLength().Should().Be(1);
        body.GetProperty("data")[0].GetProperty("netRevenue").GetDecimal().Should().Be(2000m);
    }

    [Fact]
    public async Task Query_ToolCannotExpandScopeWithUnknownCompany()
    {
        using var response = await SendQueryAsync(_tokenA, new
        {
            dataset = "sales",
            metrics = new[] { "netRevenue" },
            dimensions = new[] { "date.month" },
            filters = new[] { new { field = "company.id", @operator = "equals", values = new[] { Guid.NewGuid().ToString() } } },
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    private async Task<HttpResponseMessage> SendQueryAsync(string accessToken, object payload)
    {
        var tenantId = accessToken == _tokenA ? _tenantAId : _tenantBId;
        var before = await CountUsageAsync(tenantId);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analytics/query")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        using var toolRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/tools/queryAnalytics")
        {
            Content = JsonContent.Create(payload),
        };
        toolRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var toolResponse = await _client.SendAsync(toolRequest);
        toolResponse.StatusCode.Should().Be(response.StatusCode);
        var after = await CountUsageAsync(tenantId);
        after.Should().Be(before + (response.IsSuccessStatusCode ? 2 : 0), "cada consulta bem-sucedida deve ser medida, inclusive no cache");
        if (response.IsSuccessStatusCode)
        {
            var normal = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            var tool = await toolResponse.Content.ReadFromJsonAsync<JsonElement>();
            tool.GetProperty("tenantId").GetGuid().Should().Be(accessToken == _tokenA ? _tenantAId : _tenantBId);
            tool.GetProperty("tool").GetString().Should().Be("queryAnalytics");
            tool.GetProperty("source").GetString().Should().Be("analytics");
            tool.GetProperty("query").GetProperty("dataset").GetString().Should().Be("sales");
            foreach (var field in new[] { "data", "metadata" })
            {
                var expected = JsonNode.Parse(normal.GetProperty(field).GetRawText());
                var actual = JsonNode.Parse(tool.GetProperty(field).GetRawText());
                if (field == "metadata")
                {
                    expected!.AsObject().Remove("executedAt");
                    actual!.AsObject().Remove("executedAt");
                    tool.GetProperty("metadata").GetProperty("executedAt").GetDateTimeOffset()
                        .Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
                }

                JsonNode.DeepEquals(expected, actual).Should().BeTrue();
            }
        }

        return response;
    }

    private async Task<int> CountUsageAsync(Guid tenantId)
    {
        var accessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var previous = accessor.Current;
        accessor.Current = new TenantContext(tenantId);
        try
        {
            await using var usage = await _fixture.Services.GetRequiredService<IDbContextFactory<UsageDbContext>>().CreateDbContextAsync();
            return await usage.AnalyticsQueries.CountAsync(q => q.TenantId == tenantId);
        }
        finally
        {
            accessor.Current = previous;
        }
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

    /// <summary>Owner em ambos os tenants, para ter <c>analytics.query</c>.</summary>
    private async Task<(Guid TenantAId, Guid TenantBId)> ProvisionTenantsAndMembershipsAsync(Guid userAId, Guid userBId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var tenantDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<TenantDbContext>>();

        var tenantA = Tenant.Create("Analytics Tenant A (E1.2)", $"e1-2-a-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
        var tenantB = Tenant.Create("Analytics Tenant B (E1.2)", $"e1-2-b-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");

        tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await tenantDbFactory.CreateDbContextAsync();
            var membershipA = Membership.Create(userAId, tenantA.Id, MembershipRole.Owner);
            var membershipB = Membership.Create(userBId, tenantB.Id, MembershipRole.Owner);
            membershipA.Activate();
            membershipB.Activate();

            db.Tenants.AddRange(tenantA, tenantB);
            db.Memberships.AddRange(membershipA, membershipB);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }

        return (tenantA.Id, tenantB.Id);
    }

    private async Task SeedFactSalesInvoiceItemAsync(Guid tenantId, int dateKey, decimal netAmount)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();

        tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = await warehouseDbFactory.CreateDbContextAsync();

            var customer = DimCustomer.CreateCurrentVersion(
                tenantId, Guid.NewGuid(), $"AN-C-{Guid.NewGuid():N}", "Cliente Analytics",
                email: null, city: null, stateOrRegion: null, countryCode: null, isActive: true, DateTimeOffset.UtcNow);
            var product = DimProduct.CreateCurrentVersion(
                tenantId, Guid.NewGuid(), $"AN-P-{Guid.NewGuid():N}", "Produto Analytics", "Product",
                categoryKey: null, unitOfMeasure: null, isActive: true, DateTimeOffset.UtcNow);
            db.DimCustomers.Add(customer);
            db.DimProducts.Add(product);
            await db.SaveChangesAsync();

            var fact = FactSalesInvoiceItem.Create(
                tenantId,
                tenantKey: 1,
                companyKey: 1,
                dateKey: dateKey,
                customerKey: customer.CustomerKey,
                productKey: product.ProductKey,
                currencyKey: 1,
                sourceSystemId: Guid.NewGuid(),
                sourceEntity: "sales-invoices",
                sourceRecordId: $"NF-ANALYTICS-{Guid.NewGuid():N}-1",
                salesInvoiceId: Guid.NewGuid(),
                salesInvoiceItemId: Guid.NewGuid(),
                rawObjectUri: $"tests/{Guid.NewGuid():N}.json",
                loadBatchId: Guid.NewGuid(),
                invoiceNumber: "NF-ANALYTICS",
                status: "Issued",
                lineNumber: 1,
                quantity: 1m,
                grossAmount: netAmount,
                discountAmount: 0m,
                taxAmount: null,
                netAmount: netAmount);

            db.FactSalesInvoiceItems.Add(fact);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }
    }

    private async Task SeedSuccessfulLoadBatchAsync(Guid tenantId)
    {
        var tenantContextAccessor = _fixture.Services.GetRequiredService<ITenantContextAccessor>();
        var warehouseDbFactory = _fixture.Services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();

        tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = await warehouseDbFactory.CreateDbContextAsync();
            var batch = LoadBatch.Start(tenantId, Guid.NewGuid(), $"analytics-test-{Guid.NewGuid():N}");
            batch.Complete(itemsConsideredCount: 2, factRowsUpsertedCount: 2);
            db.LoadBatches.Add(batch);
            await db.SaveChangesAsync();
        }
        finally
        {
            tenantContextAccessor.Current = null;
        }
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
