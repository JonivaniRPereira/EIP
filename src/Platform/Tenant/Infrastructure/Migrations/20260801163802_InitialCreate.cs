using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Tenant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenant");

            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataIsolationMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DefaultTimezone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId",
                schema: "tenant",
                table: "Companies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId",
                schema: "tenant",
                table: "Memberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_UserId",
                schema: "tenant",
                table: "Memberships",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                schema: "tenant",
                table: "Tenants",
                column: "Slug",
                unique: true);

            // RLS obrigatória (ADR-007): toda tabela com TenantId nasce protegida, na mesma
            // migration que a cria — nunca em uma migration "de RLS" separada e posterior.
            // A função de predicado compara o TenantId da linha com o SESSION_CONTEXT('TenantId')
            // definido pelo TenantSessionContextInterceptor a cada abertura de conexão. Sem
            // SESSION_CONTEXT definido (NULL), a comparação falha e nenhuma linha é retornada —
            // isso é "negar por padrão" (docs/07-Seguranca.md §5.2).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION tenant.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN SELECT 1 AS fn_accesspredicate_result
                WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
                """);

            migrationBuilder.Sql(
                """
                CREATE SECURITY POLICY tenant.TenantAccessPolicy
                ADD FILTER PREDICATE tenant.fn_TenantAccessPredicate(TenantId) ON tenant.Companies,
                ADD BLOCK PREDICATE tenant.fn_TenantAccessPredicate(TenantId) ON tenant.Companies AFTER INSERT,
                ADD BLOCK PREDICATE tenant.fn_TenantAccessPredicate(TenantId) ON tenant.Companies AFTER UPDATE,
                ADD FILTER PREDICATE tenant.fn_TenantAccessPredicate(TenantId) ON tenant.Memberships,
                ADD BLOCK PREDICATE tenant.fn_TenantAccessPredicate(TenantId) ON tenant.Memberships AFTER INSERT,
                ADD BLOCK PREDICATE tenant.fn_TenantAccessPredicate(TenantId) ON tenant.Memberships AFTER UPDATE
                WITH (STATE = ON);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A policy/função precisam ser removidas antes das tabelas que protegem.
            migrationBuilder.Sql("DROP SECURITY POLICY tenant.TenantAccessPolicy;");
            migrationBuilder.Sql("DROP FUNCTION tenant.fn_TenantAccessPredicate;");

            migrationBuilder.DropTable(
                name: "Companies",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Memberships",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "tenant");
        }
    }
}
