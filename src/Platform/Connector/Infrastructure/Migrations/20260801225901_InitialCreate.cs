using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Connector.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "connector");

        migrationBuilder.CreateTable(
            name: "ConnectorInstances",
            schema: "connector",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                BaseUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConnectorInstances", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SyncRuns",
            schema: "connector",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectorInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                RecordsProcessed = table.Column<int>(type: "int", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SyncRuns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConnectorInstances_TenantId",
            schema: "connector",
            table: "ConnectorInstances",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_SyncRuns_ConnectorInstanceId",
            schema: "connector",
            table: "SyncRuns",
            column: "ConnectorInstanceId");

        migrationBuilder.CreateIndex(
            name: "IX_SyncRuns_TenantId",
            schema: "connector",
            table: "SyncRuns",
            column: "TenantId");

        // RLS obrigatória (ADR-007): toda tabela com TenantId nasce protegida, na mesma migration que
        // a cria — nunca em uma migration "de RLS" separada e posterior (mesmo padrão de
        // tenant.fn_TenantAccessPredicate/TenantAccessPolicy do módulo Tenant, InitialCreate). Cada
        // schema tem sua própria função/policy: SECURITY POLICY não é compartilhada entre módulos.
        migrationBuilder.Sql(
            """
            CREATE FUNCTION connector.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql(
            """
            CREATE SECURITY POLICY connector.ConnectorAccessPolicy
            ADD FILTER PREDICATE connector.fn_TenantAccessPredicate(TenantId) ON connector.ConnectorInstances,
            ADD BLOCK PREDICATE connector.fn_TenantAccessPredicate(TenantId) ON connector.ConnectorInstances AFTER INSERT,
            ADD BLOCK PREDICATE connector.fn_TenantAccessPredicate(TenantId) ON connector.ConnectorInstances AFTER UPDATE,
            ADD FILTER PREDICATE connector.fn_TenantAccessPredicate(TenantId) ON connector.SyncRuns,
            ADD BLOCK PREDICATE connector.fn_TenantAccessPredicate(TenantId) ON connector.SyncRuns AFTER INSERT,
            ADD BLOCK PREDICATE connector.fn_TenantAccessPredicate(TenantId) ON connector.SyncRuns AFTER UPDATE
            WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A policy/função precisam ser removidas antes das tabelas que protegem.
        migrationBuilder.Sql("DROP SECURITY POLICY connector.ConnectorAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION connector.fn_TenantAccessPredicate;");

        migrationBuilder.DropTable(
            name: "ConnectorInstances",
            schema: "connector");

        migrationBuilder.DropTable(
            name: "SyncRuns",
            schema: "connector");
    }
}
