using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Intelligence.Dashboard.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "dashboard");

        migrationBuilder.CreateTable(
            name: "Dashboards",
            schema: "dashboard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Widgets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Dashboards", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DashboardVersions",
            schema: "dashboard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DashboardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Widgets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DashboardVersions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Dashboards_TenantId",
            schema: "dashboard",
            table: "Dashboards",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_DashboardVersions_DashboardId",
            schema: "dashboard",
            table: "DashboardVersions",
            column: "DashboardId");

        migrationBuilder.CreateIndex(
            name: "IX_DashboardVersions_TenantId",
            schema: "dashboard",
            table: "DashboardVersions",
            column: "TenantId");

        // RLS obrigatória desde a primeira migration (ADR-007) — mesmo padrão exato de
        // canonical.fn_TenantAccessPredicate/canonical.CanonicalAccessPolicy (Fase 1, E2.2):
        // 1 FILTER + 2 BLOCK (AFTER INSERT/UPDATE) por tabela tenant-scoped, sem exceção.
        migrationBuilder.Sql(
            """
            CREATE FUNCTION dashboard.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql(
            """
            CREATE SECURITY POLICY dashboard.DashboardAccessPolicy
            ADD FILTER PREDICATE dashboard.fn_TenantAccessPredicate(TenantId) ON dashboard.Dashboards,
            ADD BLOCK PREDICATE dashboard.fn_TenantAccessPredicate(TenantId) ON dashboard.Dashboards AFTER INSERT,
            ADD BLOCK PREDICATE dashboard.fn_TenantAccessPredicate(TenantId) ON dashboard.Dashboards AFTER UPDATE,
            ADD FILTER PREDICATE dashboard.fn_TenantAccessPredicate(TenantId) ON dashboard.DashboardVersions,
            ADD BLOCK PREDICATE dashboard.fn_TenantAccessPredicate(TenantId) ON dashboard.DashboardVersions AFTER INSERT,
            ADD BLOCK PREDICATE dashboard.fn_TenantAccessPredicate(TenantId) ON dashboard.DashboardVersions AFTER UPDATE
            WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SECURITY POLICY dashboard.DashboardAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION dashboard.fn_TenantAccessPredicate;");

        migrationBuilder.DropTable(
            name: "Dashboards",
            schema: "dashboard");

        migrationBuilder.DropTable(
            name: "DashboardVersions",
            schema: "dashboard");
    }
}
