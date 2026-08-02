using EIP.BuildingBlocks.DDD;

namespace EIP.Data.Connector.Domain;

/// <summary>
/// Instância configurada do único Connector Type de referência da Fase 0 — REST API genérica
/// (docs/05-Connector-Framework.md §14: alta prioridade inicial, "validar o framework com contrato
/// previsível"). Protegida por RLS obrigatória (ADR-007): pertence sempre a um <see cref="TenantId"/>.
///
/// <see cref="CompanyId"/> é obrigatório: "Connector Instance: configuração de um Connector Type
/// para uma empresa, ambiente e credencial específicos" (docs/05-Connector-Framework.md §3) — todo
/// registro canônico produzido a partir desta instância herda este <c>CompanyId</c> (docs/04 §4).
///
/// <see cref="SourceEntity"/> declara qual entidade canônica esta instância sincroniza
/// (<c>"customers"</c>, <c>"products"</c>, <c>"sales-invoices"</c> na fatia Comercial da Fase 1) —
/// o Pipeline (E3) usa isso para escolher o mapeamento correto. Não existe Connector Registry
/// completo ainda (docs/roadmap/fase-1-backlog.md §3), então isso é fixo por instância, não
/// descoberto dinamicamente.
///
/// <see cref="Id"/> desta instância também funciona como o <c>SourceSystemId</c> do Modelo Canônico
/// (docs/04 §4) — não existe uma entidade "Source System" separada ainda (fica para quando o
/// Connector Registry completo/Catalog existir).
/// </summary>
public sealed class ConnectorInstance : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; }
    public string BaseUrl { get; private set; }
    public string SourceEntity { get; private set; }
    public ConnectorInstanceStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ConnectorInstance(Guid id, Guid tenantId, Guid companyId, string name, string baseUrl, string sourceEntity)
        : base(id)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        Name = name;
        BaseUrl = baseUrl;
        SourceEntity = sourceEntity;
        Status = ConnectorInstanceStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private ConnectorInstance()
    {
        Name = string.Empty;
        BaseUrl = string.Empty;
        SourceEntity = string.Empty;
    }

    public static ConnectorInstance Create(Guid tenantId, Guid companyId, string name, string baseUrl, string sourceEntity)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId é obrigatório.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntity);

        return new ConnectorInstance(Guid.NewGuid(), tenantId, companyId, name, baseUrl, sourceEntity);
    }
}
