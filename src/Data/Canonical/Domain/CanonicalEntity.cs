using EIP.BuildingBlocks.DDD;

namespace EIP.Data.Canonical.Domain;

/// <summary>
/// Campos comuns obrigatórios de toda entidade canônica (docs/04-Modelo-Canonico.md §4). Protegida
/// por RLS obrigatória (ADR-007) em todas as entidades concretas — nenhum registro canônico existe
/// sem <see cref="TenantId"/>.
///
/// <c>BranchId</c> do CDM foi deliberadamente omitido: <c>Branch</c> não existe como entidade ainda
/// (fora do escopo desta fase, docs/roadmap/fase-1-backlog.md §6).
/// </summary>
public abstract class CanonicalEntity : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid SourceSystemId { get; private set; }
    public string SourceEntity { get; private set; }
    public string SourceRecordId { get; private set; }
    public DateTimeOffset? SourceUpdatedAt { get; private set; }
    public DateTimeOffset IngestedAt { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public string SchemaVersion { get; private set; }
    public string CorrelationId { get; private set; }
    public string RawObjectUri { get; private set; }

    protected CanonicalEntity(Guid id, CanonicalLineage lineage)
        : base(id)
    {
        TenantId = lineage.TenantId;
        CompanyId = lineage.CompanyId;
        SourceSystemId = lineage.SourceSystemId;
        SourceEntity = lineage.SourceEntity;
        SourceRecordId = lineage.SourceRecordId;
        SourceUpdatedAt = lineage.SourceUpdatedAt;
        SchemaVersion = lineage.SchemaVersion;
        CorrelationId = lineage.CorrelationId;
        RawObjectUri = lineage.RawObjectUri;
        IngestedAt = DateTimeOffset.UtcNow;
        ProcessedAt = DateTimeOffset.UtcNow;
        IsDeleted = false;
    }

    protected CanonicalEntity()
    {
        SourceEntity = string.Empty;
        SourceRecordId = string.Empty;
        SchemaVersion = string.Empty;
        CorrelationId = string.Empty;
        RawObjectUri = string.Empty;
    }

    /// <summary>Exclusão lógica detectada na origem (docs/04 §4) — nunca um DELETE físico, para
    /// preservar a linhagem e permitir auditoria/reprocessamento.</summary>
    public void MarkDeleted() => IsDeleted = true;
}
