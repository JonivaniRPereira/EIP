using System.Text;
using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Infrastructure;
using EIP.Data.Connector.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Infrastructure;
using EIP.Data.Semantic.Application;
using EIP.Data.Warehouse.Application;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Intelligence.Analytics.Application;
using EIP.Platform.Identity.Application;
using EIP.Platform.Identity.Application.Abstractions;
using EIP.Platform.Identity.Domain;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Shared.Contracts.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

const string CorrelationIdHeaderName = "X-Correlation-Id";
const string ReadyTag = "ready";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "EIP.Host")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {SourceContext}: {Message:lj}{NewLine}{Exception}",
        formatProvider: System.Globalization.CultureInfo.InvariantCulture));

// ProblemDetails para toda resposta de erro (validação do [ApiController], exceções não tratadas via
// UseExceptionHandler, status codes sem corpo) — docs/03-Stack-Tecnologica.md §5.3.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        if (context.HttpContext.Items.TryGetValue(CorrelationIdHeaderName, out var correlationId))
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    };
});
builder.Services.AddExceptionHandler(_ => { });

// Contexto de tenant (AsyncLocal — singleton correto: seu estado é por fluxo assíncrono, não por
// escopo de DI; ver EIP.BuildingBlocks.Security).
builder.Services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
builder.Services.AddSingleton<TenantSessionContextInterceptor>();

// Lida sempre no momento da resolução do serviço (nunca em uma variável capturada no escopo do
// top-level, antes de `builder.Build()`) — testes de integração (`WebApplicationFactory<Program>`,
// `HostApiFixture`) sobrescrevem `ConnectionStrings:*` via `ConfigureAppConfiguration`, e essa
// sobrescrita só existe em `IConfiguration` a partir do momento em que o host é de fato construído.
// Ler antecipadamente (como antes) sempre devolvia o valor de `appsettings.json`, ignorando a
// sobrescrita do teste — bug real encontrado durante o E7.1, mascarado até agora porque o SQL Server
// do `docker-compose` local (mesma porta/credenciais do valor "dev only") normalmente estava de pé.
static string GetRequiredConnectionString(IServiceProvider services, string name) =>
    services.GetRequiredService<IConfiguration>().GetConnectionString(name)
        ?? throw new InvalidOperationException($"ConnectionStrings:{name} não configurado.");

// Única forma de obter TenantDbContext no processo: IDbContextFactory (não DbContext escopado).
// Registrar os dois ao mesmo tempo para o mesmo TContext causa erro de resolução de escopo do DI;
// a factory sozinha atende tanto controllers quanto o MembershipDirectory (que precisa abrir uma
// conexão nova para usar a sentinela de sistema, ver EIP.Platform.Tenant.Infrastructure.MembershipDirectory).
builder.Services.AddDbContextFactory<TenantDbContext>((sp, options) =>
    options.UseSqlServer(GetRequiredConnectionString(sp, "TenantDb"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

// identity.RefreshTokens tem RLS obrigatória (ADR-007) mesmo o schema `identity` em geral não sendo
// tenant-scoped — precisa do mesmo interceptor para o SESSION_CONTEXT ser definido (RefreshTokenStore
// sempre roda sob a sentinela de sistema, ver comentário na própria classe).
builder.Services.AddDbContext<AppIdentityDbContext>((sp, options) =>
    options.UseSqlServer(GetRequiredConnectionString(sp, "IdentityDb"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

// Mesmo motivo do TenantDbContext acima: só factory, nunca DbContext escopado ao mesmo tempo.
builder.Services.AddDbContextFactory<ConnectorDbContext>((sp, options) =>
    options.UseSqlServer(GetRequiredConnectionString(sp, "ConnectorDb"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddDbContextFactory<CanonicalDbContext>((sp, options) =>
    options.UseSqlServer(GetRequiredConnectionString(sp, "CanonicalDb"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddDbContextFactory<WarehouseDbContext>((sp, options) =>
    options.UseSqlServer(GetRequiredConnectionString(sp, "WarehouseDb"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddScoped<IConnectorSyncStore, ConnectorSyncStore>();
builder.Services.AddSingleton<IConnectorSyncPublisher>(sp => new RabbitMqConnectorSyncPublisher(GetRequiredConnectionString(sp, "RabbitMQ")));
builder.Services.AddScoped<IConnectorSyncService, ConnectorSyncService>();
builder.Services.AddScoped<ICanonicalRecordStore, CanonicalRecordStore>();
builder.Services.AddScoped<IWarehouseLoadStore, WarehouseLoadStore>();
builder.Services.AddScoped<IMetricsQueryService, MetricsQueryService>();
builder.Services.AddScoped<IAnalyticsQueryService, AnalyticsQueryService>();
builder.Services.AddScoped<IDeclarativeAnalyticsQueryService, DeclarativeAnalyticsQueryService>();

builder.Services.AddScoped<IMembershipDirectory, EIP.Platform.Tenant.Infrastructure.MembershipDirectory>();
builder.Services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IAuthService, AuthService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException($"Seção '{JwtOptions.SectionName}' não configurada.");
builder.Services.AddSingleton(Options.Create(jwtOptions));
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppIdentityDbContext>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Mantém os nomes de claim exatamente como emitidos (ex. "sub") em vez do remapeamento
        // legado para URIs longas de ClaimTypes.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// Autorização por permissão (E2.5) + "negar por padrão" (docs/07-Seguranca.md §5.2): qualquer
// endpoint sem [Authorize]/[AllowAnonymous] explícito exige autenticação por padrão.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Health checks: "live" nunca depende de dependências externas (não derruba o pod por uma
// dependência instável); "ready" cobre as dependências críticas do E1 (docs/14-DevOps.md §10).
builder.Services.AddHealthChecks()
    .AddSqlServer(sp => GetRequiredConnectionString(sp, "TenantDb"), name: "tenant-db", tags: [ReadyTag])
    .AddSqlServer(sp => GetRequiredConnectionString(sp, "IdentityDb"), name: "identity-db", tags: [ReadyTag])
    .AddRedis(sp => GetRequiredConnectionString(sp, "Redis"), name: "redis", tags: [ReadyTag])
    .AddRabbitMQ(
        sp => new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(GetRequiredConnectionString(sp, "RabbitMQ")) }.CreateConnectionAsync(),
        name: "rabbitmq",
        tags: [ReadyTag]);

// Observabilidade (docs/03-Stack-Tecnologica.md §10): traces/métricas básicos via OpenTelemetry.
// Exporter de Prometheus ainda está em beta no ecossistema OpenTelemetry .NET (sem GA disponível).
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("EIP.Host"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// CorrelationId precisa ser o primeiro middleware: tudo que roda depois (logs, ProblemDetails,
// controllers) já enxerga o valor (docs/03-Stack-Tecnologica.md §10 — CorrelationId em toda
// requisição/evento/job).
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var existing)
        && !string.IsNullOrWhiteSpace(existing)
        ? existing.ToString()
        : Guid.NewGuid().ToString();

    context.Items[CorrelationIdHeaderName] = correlationId;
    context.Response.Headers[CorrelationIdHeaderName] = correlationId;

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Popula o ITenantContextAccessor a partir do claim tenant_id do JWT já validado — nunca de input
// não autenticado (docs/08-Multi-Tenant.md §5.1).
app.Use(async (context, next) =>
{
    var tenantClaim = context.User.FindFirst(EipClaimTypes.TenantId)?.Value;
    if (Guid.TryParse(tenantClaim, out var tenantId))
    {
        var tenantContextAccessor = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
        tenantContextAccessor.Current = new TenantContext(tenantId);
    }

    await next();
});

// Fonte de dados de referência para o único Connector Type da Fase 0/1 (REST genérico, E7.1) — um
// stand-in local para "sistema externo", só para provar o fluxo de ponta a ponta sem depender de
// internet/terceiros. Não é uma feature de produto; um `ConnectorInstance` real aponta para o ERP/
// API de fato do cliente. Formato pensado para o mapeamento canônico da fatia Comercial (E3,
// docs/roadmap/fase-1-backlog.md) — `code` é a chave de negócio que `customerCode`/`productCode`
// referenciam em `/sample/sales-invoices`.
//
// `updatedAt` + `updatedSince` (E7.1, docs/04-Modelo-Canonico.md §11): extração incremental por
// watermark. Os registros fixos têm `updatedAt` no passado (nunca aparecem de novo depois da
// primeira sincronização); o registro "*099" tem `updatedAt` calculado a cada chamada
// (`DateTimeOffset.UtcNow`), simulando um dado que continua mudando na origem — prova que o filtro
// realmente varia entre execuções, não só na primeira.
app.MapGet("/api/v1/sample/customers", (DateTimeOffset? updatedSince) =>
{
    var records = new[]
    {
        new { code = "C001", name = "Ana Souza", email = "ana.souza@example.com", city = "São Paulo", stateOrRegion = "SP", countryCode = "BR", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero) },
        new { code = "C002", name = "Bruno Lima", email = "bruno.lima@example.com", city = "Rio de Janeiro", stateOrRegion = "RJ", countryCode = "BR", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero) },
        new { code = "C003", name = "Carla Nunes", email = "carla.nunes@example.com", city = "Belo Horizonte", stateOrRegion = "MG", countryCode = "BR", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero) },
        new { code = "C004", name = "Diego Alves", email = "diego.alves@example.com", city = "Curitiba", stateOrRegion = "PR", countryCode = "BR", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero) },
        new { code = "C005", name = "Elisa Prado", email = "elisa.prado@example.com", city = "Porto Alegre", stateOrRegion = "RS", countryCode = "BR", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero) },
        new { code = "C099", name = "Cliente Sempre Atualizado (E7.1)", email = "sempre-atualizado@example.com", city = "São Paulo", stateOrRegion = "SP", countryCode = "BR", isActive = true, updatedAt = DateTimeOffset.UtcNow },
    };

    return Results.Json(records.Where(r => updatedSince is not { } since || r.updatedAt >= since));
}).AllowAnonymous();

app.MapGet("/api/v1/sample/products", (DateTimeOffset? updatedSince) =>
{
    var records = new[]
    {
        new { code = "P001", name = "Notebook Pro 14", productType = "Product", unitOfMeasure = "UN", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero) },
        new { code = "P002", name = "Monitor UltraWide 29", productType = "Product", unitOfMeasure = "UN", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero) },
        new { code = "P003", name = "Suporte Técnico Mensal", productType = "Service", unitOfMeasure = "UN", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero) },
        new { code = "P004", name = "Licença de Software Anual", productType = "Service", unitOfMeasure = "UN", isActive = true, updatedAt = new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero) },
        new { code = "P099", name = "Produto Sempre Atualizado (E7.1)", productType = "Product", unitOfMeasure = "UN", isActive = true, updatedAt = DateTimeOffset.UtcNow },
    };

    return Results.Json(records.Where(r => updatedSince is not { } since || r.updatedAt >= since));
}).AllowAnonymous();

app.MapGet("/api/v1/sample/sales-invoices", (DateTimeOffset? updatedSince) =>
{
    var records = new[]
    {
        new
        {
            invoiceNumber = "NF-0001",
            issueDate = "2026-07-01",
            customerCode = "C001",
            status = "Issued",
            currencyCode = "BRL",
            updatedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            items = new[]
            {
                new { lineNumber = 1, productCode = "P001", quantity = 1m, unitPrice = 5200.00m, discountAmount = 0m },
                new { lineNumber = 2, productCode = "P003", quantity = 1m, unitPrice = 300.00m, discountAmount = 0m },
            },
        },
        new
        {
            invoiceNumber = "NF-0002",
            issueDate = "2026-07-03",
            customerCode = "C002",
            status = "Issued",
            currencyCode = "BRL",
            updatedAt = new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero),
            items = new[]
            {
                new { lineNumber = 1, productCode = "P002", quantity = 2m, unitPrice = 1800.00m, discountAmount = 100.00m },
            },
        },
        new
        {
            invoiceNumber = "NF-0003",
            issueDate = "2026-07-05",
            customerCode = "C003",
            status = "Issued",
            currencyCode = "BRL",
            updatedAt = new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero),
            items = new[]
            {
                new { lineNumber = 1, productCode = "P004", quantity = 1m, unitPrice = 1200.00m, discountAmount = 0m },
                new { lineNumber = 2, productCode = "P003", quantity = 3m, unitPrice = 300.00m, discountAmount = 0m },
            },
        },
        new
        {
            invoiceNumber = "NF-E7-ALWAYS",
            issueDate = "2026-07-07",
            customerCode = "C099",
            status = "Issued",
            currencyCode = "BRL",
            updatedAt = DateTimeOffset.UtcNow,
            items = new[]
            {
                new { lineNumber = 1, productCode = "P099", quantity = 1m, unitPrice = 10.00m, discountAmount = 0m },
            },
        },
    };

    return Results.Json(records.Where(r => updatedSince is not { } since || r.updatedAt >= since));
}).AllowAnonymous();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(ReadyTag),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

app.MapPrometheusScrapingEndpoint().AllowAnonymous();

app.Run();

/// <summary>Marcador necessário para <c>WebApplicationFactory&lt;Program&gt;</c> em testes de
/// integração em nível de API (E2.6) — Program.cs top-level statements geram uma classe `Program`
/// implícita `internal`; isso a expõe como `public partial`.</summary>
public partial class Program;

/// <summary>Resposta JSON com o status de cada dependência, em vez do texto plano padrão — mais
/// útil para diagnosticar qual dependência específica caiu (docs/14-DevOps.md §10).</summary>
internal static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
            }),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}
