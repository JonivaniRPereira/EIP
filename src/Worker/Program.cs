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
using EIP.Data.Warehouse.Application;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Shared.Contracts.Tenancy;
using EIP.Worker.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
if (!builder.Environment.IsDevelopment())
{
    foreach (var name in new[] { "TenantDb", "ConnectorDb", "CanonicalDb", "WarehouseDb" })
    {
        var value = builder.Configuration.GetConnectionString(name) ?? string.Empty;
        var sql = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(value);
        if (value.Contains("Dev_Only", StringComparison.OrdinalIgnoreCase) || sql.UserID.Equals("sa", StringComparison.OrdinalIgnoreCase)
            || sql.TrustServerCertificate || sql.Encrypt.ToString() is not ("True" or "Mandatory" or "Strict"))
        {
            throw new InvalidOperationException("Worker exige conexões SQL restritas e TLS fora de Development.");
        }
    }

    var secret = builder.Configuration["DataLake:SecretKey"] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(secret) || secret.Contains("Dev_Only", StringComparison.OrdinalIgnoreCase)
        || !Uri.TryCreate(builder.Configuration["DataLake:ServiceUrl"], UriKind.Absolute, out var storage) || storage.Scheme != "https"
        || !Uri.TryCreate(builder.Configuration.GetConnectionString("RabbitMQ"), UriKind.Absolute, out var broker) || broker.Scheme != "amqps"
        || (builder.Configuration.GetConnectionString("RabbitMQ")?.Contains("Dev_Only", StringComparison.OrdinalIgnoreCase) ?? true)
        || builder.Configuration.GetSection("ReferenceRest:AllowedEndpoints").Get<string[]>()?.Any(url => !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) == true)
    {
        throw new InvalidOperationException("Worker exige configuração segura de storage, broker e destinos fora de Development.");
    }
}

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
var warehouseConnectionString = builder.Configuration.GetConnectionString("WarehouseDb")
    ?? throw new InvalidOperationException("ConnectionStrings:WarehouseDb não configurado.");
var tenantConnectionString = builder.Configuration.GetConnectionString("TenantDb")
    ?? throw new InvalidOperationException("ConnectionStrings:TenantDb não configurado.");
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

builder.Services.AddDbContextFactory<WarehouseDbContext>((sp, options) =>
    options.UseSqlServer(warehouseConnectionString)
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddDbContextFactory<TenantDbContext>((sp, options) =>
    options.UseSqlServer(tenantConnectionString)
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddSingleton<IConnectorSyncStore, ConnectorSyncStore>();
builder.Services.AddSingleton<ICanonicalRecordStore, CanonicalRecordStore>();
builder.Services.AddSingleton<ICanonicalReconciliationService, CanonicalReconciliationService>();
builder.Services.AddSingleton<ITenantDirectory, TenantDirectory>();
builder.Services.AddSingleton<IWarehouseLoadStore, WarehouseLoadStore>();
builder.Services.AddSingleton<IWarehouseLoadService, WarehouseLoadService>();
builder.Services.AddSingleton<IWarehouseReconciliationService, WarehouseReconciliationService>();

var dataLakeOptions = builder.Configuration.GetSection(S3RawObjectStoreOptions.SectionName).Get<S3RawObjectStoreOptions>()
    ?? throw new InvalidOperationException("Seção de configuração 'DataLake' não configurada.");
builder.Services.AddRawObjectStore(dataLakeOptions);

builder.Services.AddSingleton<IPipelineProcessor, PipelineProcessor>();

builder.Services.AddReferenceRestHttpClient();
builder.Services.AddOptions<ReferenceRestOptions>().BindConfiguration("ReferenceRest")
    .Validate(options => options.MaxResponseBytes is > 0 and <= 67108864, "Limite de resposta do conector inválido.").ValidateOnStart();
builder.Services.AddSingleton<IReferenceEndpointPolicy, ReferenceEndpointPolicy>();
builder.Services.AddSingleton<IReferenceRestClient, HttpReferenceRestClient>();

builder.Services.AddSingleton<IConnectorSyncProcessor, ConnectorSyncProcessor>();

builder.Services.AddSingleton(new RabbitMqConnectionOptions(rabbitMqConnectionString));
builder.Services.AddSingleton(ConnectorRetryOptions.Default);
builder.Services.AddHostedService<SyncRequestedConsumerService>();

var host = builder.Build();
await host.RunAsync();
