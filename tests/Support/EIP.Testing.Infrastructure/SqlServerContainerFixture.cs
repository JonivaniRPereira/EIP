using EIP.Data.Canonical.Infrastructure;
using EIP.Data.Connector.Infrastructure;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Intelligence.Dashboard.Infrastructure;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace EIP.Testing.Infrastructure;

/// <summary>Imagem usada tanto pelo E1 (Docker Compose) quanto pelos testes — mesma versão do
/// SQL Server em todos os ambientes.</summary>
public static class TestImages
{
    public const string SqlServer = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    /// <summary>Mesma tag pinada usada em <c>deploy/docker-compose/docker-compose.yml</c> (E1, Fase 0).</summary>
    public const string Redis = "redis:7.4-alpine";

    /// <summary>Mesma tag pinada usada em <c>deploy/docker-compose/docker-compose.yml</c> — inclui o
    /// plugin de management (não usado pelos testes, que falam AMQP puro, mas mantém a imagem idêntica
    /// à de produção).</summary>
    public const string RabbitMq = "rabbitmq:3.13-management-alpine";
}

/// <summary>Aplica as migrations dos módulos Tenant e Identity — incluindo a RLS obrigatória
/// (ADR-007) — contra uma string de conexão qualquer. Reutilizado tanto por
/// <see cref="SqlServerContainerFixture"/> quanto por fixtures específicas de outros projetos de
/// teste que também precisem de um SQL Server efêmero com o schema completo.</summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAllAsync(string connectionString)
    {
        var tenantOptions = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", TenantDbContext.Schema))
            .Options;
        await using (var tenantDb = new TenantDbContext(tenantOptions))
        {
            await tenantDb.Database.MigrateAsync();
        }

        var identityOptions = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AppIdentityDbContext.Schema))
            .Options;
        await using (var identityDb = new AppIdentityDbContext(identityOptions))
        {
            await identityDb.Database.MigrateAsync();
        }

        var connectorOptions = new DbContextOptionsBuilder<ConnectorDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ConnectorDbContext.Schema))
            .Options;
        await using (var connectorDb = new ConnectorDbContext(connectorOptions))
        {
            await connectorDb.Database.MigrateAsync();
        }

        var canonicalOptions = new DbContextOptionsBuilder<CanonicalDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", CanonicalDbContext.Schema))
            .Options;
        await using (var canonicalDb = new CanonicalDbContext(canonicalOptions))
        {
            await canonicalDb.Database.MigrateAsync();
        }

        var warehouseOptions = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", WarehouseDbContext.Schema))
            .Options;
        await using (var warehouseDb = new WarehouseDbContext(warehouseOptions))
        {
            await warehouseDb.Database.MigrateAsync();
        }

        var dashboardOptions = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DashboardDbContext.Schema))
            .Options;
        await using (var dashboardDb = new DashboardDbContext(dashboardOptions))
        {
            await dashboardDb.Database.MigrateAsync();
        }
    }
}

/// <summary>
/// Dependência efêmera para testes de integração (docs/03-Stack-Tecnologica.md §3 —
/// "xUnit + FluentAssertions + Testcontainers"; docs/14-DevOps.md §6). Sobe um SQL Server real
/// isolado por execução de teste (mesma imagem do E1), aplica as migrations dos módulos Tenant e
/// Identity e descarta o container ao final.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(TestImages.SqlServer).Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "EIP",
        };
        ConnectionString = builder.ConnectionString;

        await DatabaseMigrator.MigrateAllAsync(ConnectionString);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>Um único container compartilhado entre todas as classes de teste desta coleção — subir
/// um SQL Server por classe seria correto porém lento demais para o dia a dia.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SqlServer";
}

/// <summary>
/// RabbitMQ real e efêmero (Testcontainers) para os testes de retry/DLQ do conector de referência
/// (E2.1/E2.2 da Fase 3) — antes desta fase, `RabbitMQ` nos testes era só uma connection string
/// sintaticamente válida (ver <c>HostApiFixture</c>), suficiente enquanto nada exercitava a fila de
/// verdade. Um container por classe de teste (não compartilhado): declarar a mesma topologia mais de
/// uma vez com <see cref="EIP.Data.Connector.Infrastructure.ConnectorRetryOptions"/> diferentes entre
/// testes causaria PRECONDITION_FAILED se reaproveitassem o mesmo broker.
/// </summary>
public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder(TestImages.RabbitMq).Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
