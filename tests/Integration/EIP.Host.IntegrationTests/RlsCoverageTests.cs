using FluentAssertions;
using Microsoft.Data.SqlClient;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Gate automatizado exigido pela ADR-007 e pelo critério de saída da Fase 0: "toda tabela com
/// TenantId possui política RLS ativa, e o CI falha se alguma migration criar uma tabela de tenant
/// sem RLS correspondente". Consulta o catálogo de sistema do SQL Server diretamente (não o EF Core)
/// para enumerar toda tabela com uma coluna <c>TenantId</c> e falhar se ela não tiver uma
/// <c>SECURITY POLICY</c> habilitada com um FILTER PREDICATE aplicado — não importa qual módulo a
/// criou nem se este teste conhece o módulo. É a proteção estrutural para qualquer tabela nova, sem
/// precisar de um teste de isolamento dedicado por módulo (embora esses continuem valendo como
/// defesa em profundidade, ver <see cref="CrossTenantApiIsolationTests"/>).
/// </summary>
[Collection(HostApiCollection.Name)]
public sealed class RlsCoverageTests
{
    private readonly HostApiFixture _fixture;

    public RlsCoverageTests(HostApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Toda_tabela_com_TenantId_deve_ter_uma_politica_RLS_ativa()
    {
        const string query = """
            SELECT s.name AS SchemaName, t.name AS TableName
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.columns c ON c.object_id = t.object_id AND c.name = N'TenantId'
            WHERE NOT EXISTS (
                SELECT 1
                FROM sys.security_predicates sp
                JOIN sys.security_policies pol ON pol.object_id = sp.object_id AND pol.is_enabled = 1
                WHERE sp.target_object_id = t.object_id
                  AND sp.predicate_type_desc = 'FILTER'
            );
            """;

        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var unprotectedTables = new List<string>();
        while (await reader.ReadAsync())
        {
            unprotectedTables.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        unprotectedTables.Should().BeEmpty(
            "toda tabela com TenantId precisa de uma SECURITY POLICY ativa na mesma migration que a cria (ADR-007)");
    }
}
