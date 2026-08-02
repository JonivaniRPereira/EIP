using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Infrastructure;
using EIP.Data.Connector.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Infrastructure;
using EIP.Data.DataLake;
using EIP.Data.DataLake.Infrastructure;
using EIP.Data.Pipeline;
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
var canonicalConnectionString = builder.Configuration.GetConnectionString("CanonicalDb")
    ?? throw new InvalidOperationException("ConnectionStrings:CanonicalDb não configurado.");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMQ não configurado.");

// Contexto de tenant vindo da mensagem (não de um request HTTP) — mesmo mecanismo AsyncLocal do
// Host, ver EIP.BuildingBlocks.Security.
builder.Services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
builder.Services.AddSingleton<TenantSessionContextInterceptor>();

builder.Services.AddDbContextFactory<ConnectorDbContext>((sp, options) =>
    options.UseSqlServer(connectorConnectionString)
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddDbContextFactory<CanonicalDbContext>((sp, options) =>
    options.UseSqlServer(canonicalConnectionString)
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddSingleton<IConnectorSyncStore, ConnectorSyncStore>();
builder.Services.AddSingleton<ICanonicalRecordStore, CanonicalRecordStore>();
builder.Services.AddSingleton<ICanonicalReconciliationService, CanonicalReconciliationService>();

var dataLakeOptions = builder.Configuration.GetSection(S3RawObjectStoreOptions.SectionName).Get<S3RawObjectStoreOptions>()
    ?? throw new InvalidOperationException("Seção de configuração 'DataLake' não configurada.");
builder.Services.AddRawObjectStore(dataLakeOptions);

builder.Services.AddSingleton<IPipelineProcessor, PipelineProcessor>();

builder.Services.AddHttpClient(HttpReferenceRestClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IReferenceRestClient, HttpReferenceRestClient>();

builder.Services.AddSingleton<IConnectorSyncProcessor, ConnectorSyncProcessor>();

builder.Services.AddSingleton(new RabbitMqConnectionOptions(rabbitMqConnectionString));
builder.Services.AddHostedService<SyncRequestedConsumerService>();

var host = builder.Build();
await host.RunAsync();
