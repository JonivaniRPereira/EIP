using EIP.Platform.Connector.Infrastructure;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace EIP.Testing.Infrastructure;

/// <summary>Imagem usada tanto pelo E1 (Docker Compose) quanto pelos testes — mesma versão do
/// SQL Server em todos os ambientes.</summary>
public static class TestImages
{
    public const string SqlServer = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
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
