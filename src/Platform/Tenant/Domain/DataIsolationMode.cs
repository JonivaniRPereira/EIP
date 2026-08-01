namespace EIP.Platform.Tenant.Domain;

/// <summary>Estratégia de isolamento de dados do tenant (docs/08-Multi-Tenant.md §7). A Fase 0 só
/// implementa o modo Shared; Dedicated é modelado aqui para não exigir migração de schema depois.</summary>
public enum DataIsolationMode
{
    Shared,
    Dedicated,
}
