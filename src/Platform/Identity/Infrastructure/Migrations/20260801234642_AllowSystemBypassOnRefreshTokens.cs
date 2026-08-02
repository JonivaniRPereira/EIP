using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AllowSystemBypassOnRefreshTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A migration anterior (AddRlsToRefreshTokens) esqueceu o bypass de sistema — RefreshTokenStore
        // sempre roda sob a sentinela TenantContext.System (SESSION_CONTEXT = '00000000-...-000000000001'),
        // nunca o TenantId real da linha, então o BLOCK predicate rejeitava toda emissão/renovação de
        // token com tenant já selecionado (erro 33504). Mesmo ajuste já aplicado em
        // tenant.AllowSystemTenantBypass — função é SCHEMABINDING, então precisa dropar a policy antes.
        migrationBuilder.Sql("DROP SECURITY POLICY [identity].RefreshTokenAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION [identity].fn_TenantAccessPredicate;");

        migrationBuilder.Sql(
            """
            CREATE FUNCTION [identity].fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId IS NULL
               OR CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier) = '00000000-0000-0000-0000-000000000001'
               OR @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql(
            """
            CREATE SECURITY POLICY [identity].RefreshTokenAccessPolicy
            ADD FILTER PREDICATE [identity].fn_TenantAccessPredicate(TenantId) ON [identity].RefreshTokens,
            ADD BLOCK PREDICATE [identity].fn_TenantAccessPredicate(TenantId) ON [identity].RefreshTokens AFTER INSERT,
            ADD BLOCK PREDICATE [identity].fn_TenantAccessPredicate(TenantId) ON [identity].RefreshTokens AFTER UPDATE
            WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SECURITY POLICY [identity].RefreshTokenAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION [identity].fn_TenantAccessPredicate;");

        migrationBuilder.Sql(
            """
            CREATE FUNCTION [identity].fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId IS NULL
               OR @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql(
            """
            CREATE SECURITY POLICY [identity].RefreshTokenAccessPolicy
            ADD FILTER PREDICATE [identity].fn_TenantAccessPredicate(TenantId) ON [identity].RefreshTokens,
            ADD BLOCK PREDICATE [identity].fn_TenantAccessPredicate(TenantId) ON [identity].RefreshTokens AFTER INSERT,
            ADD BLOCK PREDICATE [identity].fn_TenantAccessPredicate(TenantId) ON [identity].RefreshTokens AFTER UPDATE
            WITH (STATE = ON);
            """);
    }
}
