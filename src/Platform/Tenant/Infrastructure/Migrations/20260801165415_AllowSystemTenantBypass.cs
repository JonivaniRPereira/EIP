using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Tenant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowSystemTenantBypass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Permite um bypass controlado da RLS para código de sistema de confiança (nunca para
            // input de cliente) — necessário para o login resolver "quais tenants este usuário
            // pertence" antes de qualquer TenantId ser conhecido (docs/07-Seguranca.md §6.1: bypass
            // administrativo requer permissão de plataforma explícita, o que a sentinela representa).
            // A função é referenciada pela policy (SCHEMABINDING), então é preciso remover a policy,
            // recriar a função e reaplicar a policy.
            migrationBuilder.Sql("DROP SECURITY POLICY tenant.TenantAccessPolicy;");
            migrationBuilder.Sql("DROP FUNCTION tenant.fn_TenantAccessPredicate;");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION tenant.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN SELECT 1 AS fn_accesspredicate_result
                WHERE CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier) = '00000000-0000-0000-0000-000000000001'
                   OR @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
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
            migrationBuilder.Sql("DROP SECURITY POLICY tenant.TenantAccessPolicy;");
            migrationBuilder.Sql("DROP FUNCTION tenant.fn_TenantAccessPredicate;");

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
    }
}
