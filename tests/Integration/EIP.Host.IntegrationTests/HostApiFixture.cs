using EIP.Data.Semantic.Application;
using EIP.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Sobe um SQL Server e um Redis efêmeros (Testcontainers, docs/03 §3), aplica as migrations dos
/// módulos Tenant e Identity, e constrói o Host real (<see cref="WebApplicationFactory{Program}"/>)
/// apontando para eles — nada de infraestrutura pré-existente precisa estar de pé, nem local nem no
/// CI (E5). Redis é real (não só health check) desde a Fase 2, E1.4 — o Analytics Engine cacheia de
/// verdade e os testes de cache (<c>AnalyticsQueryCacheTests</c>) precisam de um backend alcançável.
///
/// RabbitMQ continua recebendo uma string de conexão só sintaticamente válida (não alcançável): os
/// testes aqui não chamam `/health/ready` nem disparam sincronização de conector via fila.
/// </summary>
public sealed class HostApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(TestImages.SqlServer).Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder(TestImages.Redis).Build();
    private WebApplicationFactory<Program>? _factory;

    public HttpClient CreateClient() => Factory.CreateClient();

    public IServiceProvider Services => Factory.Services;

    /// <summary>Connection string do SQL Server efêmero — usada por testes que precisam consultar o
    /// catálogo de sistema diretamente (ex.: <see cref="RlsCoverageTests"/>), não só via EF Core.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Connection string do Redis efêmero — usada por testes que precisam montar sua própria
    /// <see cref="WebApplicationFactory{Program}"/> reaproveitando a mesma infraestrutura já de pé
    /// (ex.: <c>AnalyticsQueryTimeoutTests</c>, que precisa de um `Analytics:QueryTimeoutMilliseconds`
    /// diferente do resto da coleção).</summary>
    public string RedisConnectionString { get; private set; } = string.Empty;

    private WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException($"{nameof(HostApiFixture)} ainda não foi inicializada.");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_container.StartAsync(), _redisContainer.StartAsync());

        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "EIP",
        };
        var connectionString = builder.ConnectionString;
        ConnectionString = connectionString;
        RedisConnectionString = _redisContainer.GetConnectionString();

        await DatabaseMigrator.MigrateAllAsync(connectionString);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHostBuilder =>
        {
            webHostBuilder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("ConnectionStrings:TenantDb", connectionString),
                new("ConnectionStrings:IdentityDb", connectionString),
                new("ConnectionStrings:ConnectorDb", connectionString),
                new("ConnectionStrings:CanonicalDb", connectionString),
                new("ConnectionStrings:WarehouseDb", connectionString),
                new("ConnectionStrings:DashboardDb", connectionString),
                new("ConnectionStrings:UsageDb", connectionString),
                new("ConnectionStrings:Redis", _redisContainer.GetConnectionString()),
                new("ConnectionStrings:RabbitMQ", "amqp://guest:guest@localhost:16599/"),
            ]));

            // Decora IAnalyticsQueryService só para contar chamadas (E1.4) — prova cache hit/miss sem
            // depender de nenhum detalhe interno do Redis; nunca muda o resultado da consulta.
            webHostBuilder.ConfigureTestServices(services =>
            {
                services.AddSingleton<AnalyticsQueryCallCounter>();
                services.AddScoped<AnalyticsQueryService>();
                services.AddScoped<IAnalyticsQueryService>(sp => new CountingAnalyticsQueryService(
                    sp.GetRequiredService<AnalyticsQueryService>(),
                    sp.GetRequiredService<AnalyticsQueryCallCounter>()));
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Task.WhenAll(_container.DisposeAsync().AsTask(), _redisContainer.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class HostApiCollection : ICollectionFixture<HostApiFixture>
{
    public const string Name = "HostApi";
}
