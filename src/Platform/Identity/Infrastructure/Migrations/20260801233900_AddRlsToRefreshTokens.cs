using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRlsToRefreshTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // RLS obrigatória (ADR-007) também se aplica a identity.RefreshTokens, apesar de o schema
        // `identity` em geral não ser tenant-scoped: a tabela guarda um TenantId (nulo até o tenant
        // ser selecionado). Diferente de tenant.fn_TenantAccessPredicate/connector.fn_TenantAccessPredicate,
        // esta função deixa passar linhas com TenantId NULL incondicionalmente — são tokens ainda sem
        // tenant selecionado, e não há um tenant real para isolar ali. Toda operação em
        // identity.RefreshTokens roda sob a sentinela TenantContext.System (RefreshTokenStore), porque
        // a busca por hash acontece antes de qualquer tenant estar em contexto.
        // `identity` é palavra reservada/contextual do T-SQL (propriedade de coluna IDENTITY) — o nome
        // do schema precisa vir entre colchetes em todo lugar aqui, diferente de `tenant`/`connector`
        // (que não colidem com nenhuma palavra-chave e por isso não precisaram disso).
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SECURITY POLICY [identity].RefreshTokenAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION [identity].fn_TenantAccessPredicate;");
    }
}
