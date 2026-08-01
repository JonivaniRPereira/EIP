namespace EIP.BuildingBlocks.Security;

/// <summary>
/// Contexto de tenant autenticado da requisição/operação atual (docs/08-Multi-Tenant.md §5).
/// Nunca deve ser construído a partir de input não validado do cliente (body/query/header).
/// </summary>
public sealed record TenantContext(Guid TenantId)
{
    /// <summary>
    /// Sentinela reservada que a RLS do banco trata como "acesso de sistema" (bypass controlado da
    /// policy — docs/07-Seguranca.md §6.1: "consultas administrativas não podem burlar o filtro sem
    /// uma permissão de plataforma explícita"). Só pode ser atribuída por código interno de
    /// confiança (ex.: <c>MembershipDirectory</c> ao resolver os tenants de um usuário no login) —
    /// nunca a partir de input de cliente.
    /// </summary>
    public static readonly Guid SystemTenantId = new("00000000-0000-0000-0000-000000000001");

    public static TenantContext System { get; } = new(SystemTenantId);
}

public interface ITenantContextAccessor
{
    TenantContext? Current { get; set; }
}

/// <summary>
/// Implementação baseada em AsyncLocal: funciona tanto no pipeline HTTP (um fluxo assíncrono por
/// requisição) quanto em workers, sem acoplar o BuildingBlocks a ASP.NET Core.
/// </summary>
public sealed class AsyncLocalTenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContext?> CurrentContext = new();

    public TenantContext? Current
    {
        get => CurrentContext.Value;
        set => CurrentContext.Value = value;
    }
}
