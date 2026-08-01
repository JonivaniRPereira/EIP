using System.Text;
using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
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

var tenantConnectionString = builder.Configuration.GetConnectionString("TenantDb")
    ?? throw new InvalidOperationException("ConnectionStrings:TenantDb não configurado.");
var identityConnectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityDb não configurado.");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis não configurado.");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMQ não configurado.");

// Única forma de obter TenantDbContext no processo: IDbContextFactory (não DbContext escopado).
// Registrar os dois ao mesmo tempo para o mesmo TContext causa erro de resolução de escopo do DI;
// a factory sozinha atende tanto controllers quanto o MembershipDirectory (que precisa abrir uma
// conexão nova para usar a sentinela de sistema, ver EIP.Platform.Tenant.Infrastructure.MembershipDirectory).
builder.Services.AddDbContextFactory<TenantDbContext>((sp, options) =>
    options.UseSqlServer(tenantConnectionString)
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddDbContext<AppIdentityDbContext>(options => options.UseSqlServer(identityConnectionString));

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
    .AddSqlServer(tenantConnectionString, name: "tenant-db", tags: [ReadyTag])
    .AddSqlServer(identityConnectionString, name: "identity-db", tags: [ReadyTag])
    .AddRedis(redisConnectionString, name: "redis", tags: [ReadyTag])
    .AddRabbitMQ(
        _ => new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) }.CreateConnectionAsync(),
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
