using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Connector.Application;
using EIP.Platform.Connector.Application.Abstractions;
using EIP.Platform.Connector.Infrastructure;
using EIP.Worker.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(loggerConfiguration => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "EIP.Worker.Sync")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        formatProvider: System.Globalization.CultureInfo.InvariantCulture));

var connectorConnectionString = builder.Configuration.GetConnectionString("ConnectorDb")
    ?? throw new InvalidOperationException("ConnectionStrings:ConnectorDb não configurado.");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMQ não configurado.");

// Contexto de tenant vindo da mensagem (não de um request HTTP) — mesmo mecanismo AsyncLocal do
// Host, ver EIP.BuildingBlocks.Security.
builder.Services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
builder.Services.AddSingleton<TenantSessionContextInterceptor>();

builder.Services.AddDbContextFactory<ConnectorDbContext>((sp, options) =>
    options.UseSqlServer(connectorConnectionString)
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddSingleton<IConnectorSyncStore, ConnectorSyncStore>();

builder.Services.AddHttpClient(HttpReferenceRestClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IReferenceRestClient, HttpReferenceRestClient>();

builder.Services.AddSingleton<IConnectorSyncProcessor, ConnectorSyncProcessor>();

builder.Services.AddSingleton(new RabbitMqConnectionOptions(rabbitMqConnectionString));
builder.Services.AddHostedService<SyncRequestedConsumerService>();

var host = builder.Build();
await host.RunAsync();
