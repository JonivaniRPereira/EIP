using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Administration.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialUsage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "administration");

        migrationBuilder.CreateTable(
            name: "AnalyticsQueries",
            schema: "administration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnalyticsQueries", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AnalyticsQueries_TenantId_OccurredAt",
            schema: "administration",
            table: "AnalyticsQueries",
            columns: new[] { "TenantId", "OccurredAt" });
        migrationBuilder.Sql("""
            CREATE FUNCTION administration.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE WITH SCHEMABINDING AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);
        migrationBuilder.Sql("""
            CREATE SECURITY POLICY administration.UsageAccessPolicy
            ADD FILTER PREDICATE administration.fn_TenantAccessPredicate(TenantId) ON administration.AnalyticsQueries,
            ADD BLOCK PREDICATE administration.fn_TenantAccessPredicate(TenantId) ON administration.AnalyticsQueries AFTER INSERT,
            ADD BLOCK PREDICATE administration.fn_TenantAccessPredicate(TenantId) ON administration.AnalyticsQueries AFTER UPDATE
            WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SECURITY POLICY administration.UsageAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION administration.fn_TenantAccessPredicate;");
        migrationBuilder.DropTable(
            name: "AnalyticsQueries",
            schema: "administration");
    }
}
