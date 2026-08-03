using EIP.Testing.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Sobe um SQL Server efêmero (Testcontainers, docs/03 §3), aplica as migrations dos módulos Tenant
/// e Identity, e constrói o Host real (<see cref="WebApplicationFactory{Program}"/>) apontando para
/// ele — nada de infraestrutura pré-existente precisa estar de pé, nem local nem no CI (E5).
///
/// Redis/RabbitMQ recebem strings de conexão só sintaticamente válidas (não alcançáveis): os testes
/// aqui não chamam `/health/ready`, então essas dependências nunca são efetivamente contatadas.
/// </summary>
public sealed class HostApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(TestImages.SqlServer).Build();
    private WebApplicationFactory<Program>? _factory;

    public HttpClient CreateClient() => Factory.CreateClient();

    public IServiceProvider Services => Factory.Services;

    /// <summary>Connection string do SQL Server efêmero — usada por testes que precisam consultar o
    /// catálogo de sistema diretamente (ex.: <see cref="RlsCoverageTests"/>), não só via EF Core.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    private WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException($"{nameof(HostApiFixture)} ainda não foi inicializada.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "EIP",
        };
        var connectionString = builder.ConnectionString;
        ConnectionString = connectionString;

        await DatabaseMigrator.MigrateAllAsync(connectionString);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHostBuilder =>
            webHostBuilder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("ConnectionStrings:TenantDb", connectionString),
                new("ConnectionStrings:IdentityDb", connectionString),
                new("ConnectionStrings:ConnectorDb", connectionString),
                new("ConnectionStrings:CanonicalDb", connectionString),
                new("ConnectionStrings:WarehouseDb", connectionString),
                new("ConnectionStrings:Redis", "localhost:16399"),
                new("ConnectionStrings:RabbitMQ", "amqp://guest:guest@localhost:16599/"),
            ])));
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class HostApiCollection : ICollectionFixture<HostApiFixture>
{
    public const string Name = "HostApi";
}
