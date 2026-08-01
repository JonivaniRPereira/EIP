using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

const string CorrelationIdHeaderName = "X-Correlation-Id";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "EIP.Gateway")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {SourceContext}: {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture));

// Ponto único de entrada externo (docs/02-Arquitetura.md §Gateway); roteia só /api/** — health
// checks e métricas são acessados diretamente pela infra (probes/scraper), não por clientes.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// CORS: o navegador fala só com o Gateway (nunca direto com o Host — docs/02-Arquitetura.md
// §Gateway), então é aqui que a política precisa existir. Sem isso, o preflight OPTIONS do
// navegador nem chega a ser respondido corretamente (o proxy encaminharia para o Host, que rejeita
// por falta de autenticação — CORS precisa ser resolvido antes do proxy).
const string FrontendCorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Rate limiting básico por IP (docs/roadmap/fase-0-backlog.md E4.2) — "negar por padrão" quando o
// limite estoura, nunca deixar passar silenciosamente.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(10),
            PermitLimit = 100,
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

// CorrelationId aceito ou gerado no Gateway — o ponto de entrada real da plataforma
// (docs/08-Multi-Tenant.md §5.1) — e propagado para o backend via header (YARP encaminha headers
// do cliente por padrão, então basta garantir que o header exista antes do proxy).
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var existing)
        && !string.IsNullOrWhiteSpace(existing)
        ? existing.ToString()
        : Guid.NewGuid().ToString();

    context.Request.Headers[CorrelationIdHeaderName] = correlationId;
    context.Response.OnStarting(() =>
    {
        // Definido em OnStarting (não antes do next()): o proxy YARP copia os headers de resposta
        // do Host, então setar aqui garante um único valor final, sem duplicar o header.
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;
        return Task.CompletedTask;
    });

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

// Autenticação/autorização de negócio continuam responsabilidade dos módulos (docs/03 §5.4): o
// Gateway só encaminha o header Authorization como recebido — YARP faz isso por padrão.
app.MapReverseProxy();

app.Run();

public partial class Program;
