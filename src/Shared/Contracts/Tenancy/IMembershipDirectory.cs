namespace EIP.Shared.Contracts.Tenancy;

/// <summary>
/// Contrato de comunicação entre domínios (docs/02-Arquitetura.md §9.2): o domínio Identity nunca
/// acessa a persistência do domínio Tenant diretamente. Em vez disso depende apenas desta
/// abstração, definida em Shared/Contracts e implementada pelo módulo Tenant.
/// </summary>
public interface IMembershipDirectory
{
    /// <summary>Memberships ativas do usuário, usado no login para decidir se o tenant pode ser
    /// selecionado automaticamente ou se requer seleção explícita (docs/08-Multi-Tenant.md §5.2).</summary>
    Task<IReadOnlyList<MembershipSummary>> GetActiveMembershipsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Retorna a membership ativa do usuário no tenant informado (nula se não existir/não
    /// estiver ativa) — usado tanto para confirmar acesso quanto para resolver o papel/permissões
    /// antes de emitir um token com aquele TenantId como claim.</summary>
    Task<MembershipSummary?> GetActiveMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
