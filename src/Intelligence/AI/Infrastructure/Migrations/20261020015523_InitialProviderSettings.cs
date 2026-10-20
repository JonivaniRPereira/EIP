using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Intelligence.AI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialProviderSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "ai");

        migrationBuilder.CreateTable(
            name: "ProviderSettings",
            schema: "ai",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderSettings", x => x.TenantId);
            });
        migrationBuilder.Sql("""
            CREATE FUNCTION ai.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE WITH SCHEMABINDING AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);
        migrationBuilder.Sql("""
            CREATE SECURITY POLICY ai.ProviderSettingsAccessPolicy
            ADD FILTER PREDICATE ai.fn_TenantAccessPredicate(TenantId) ON ai.ProviderSettings,
            ADD BLOCK PREDICATE ai.fn_TenantAccessPredicate(TenantId) ON ai.ProviderSettings AFTER INSERT,
            ADD BLOCK PREDICATE ai.fn_TenantAccessPredicate(TenantId) ON ai.ProviderSettings AFTER UPDATE
            WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SECURITY POLICY ai.ProviderSettingsAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION ai.fn_TenantAccessPredicate;");
        migrationBuilder.DropTable(
            name: "ProviderSettings",
            schema: "ai");
    }
}
