using EIP.BuildingBlocks.DDD;

namespace EIP.Data.Connector.Domain;

/// <summary>
/// Instância configurada do único Connector Type de referência da Fase 0 — REST API genérica
/// (docs/05-Connector-Framework.md §14: alta prioridade inicial, "validar o framework com contrato
/// previsível"). Protegida por RLS obrigatória (ADR-007): pertence sempre a um <see cref="TenantId"/>.
/// </summary>
public sealed class ConnectorInstance : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string BaseUrl { get; private set; }
    public ConnectorInstanceStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ConnectorInstance(Guid id, Guid tenantId, string name, string baseUrl)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        BaseUrl = baseUrl;
        Status = ConnectorInstanceStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private ConnectorInstance()
    {
        Name = string.Empty;
        BaseUrl = string.Empty;
    }

    public static ConnectorInstance Create(Guid tenantId, string name, string baseUrl)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        return new ConnectorInstance(Guid.NewGuid(), tenantId, name, baseUrl);
    }
}
