using EIP.BuildingBlocks.DDD;

namespace EIP.Data.Canonical.Domain;

/// <summary>
/// Registro de quarentena (docs/04-Modelo-Canonico.md §8.2): dado bruto que falhou validação nunca
/// vira registro canônico nem chega ao Warehouse. Guarda o suficiente para diagnóstico e
/// reprocessamento manual, preservando a trilha de auditoria — motivo, regra que falhou, referência
/// ao objeto bruto, execução e correlação.
///
/// Não é um <see cref="CanonicalEntity"/>: um registro rejeitado pode não ter conseguido resolver
/// nem os campos comuns mínimos (ex. <c>CompanyId</c> por referência não resolvida, docs/04 §6.3) —
/// por isso só carrega o que é garantidamente conhecido no momento da rejeição.
/// </summary>
public sealed class CanonicalQuarantineEntry : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ConnectorInstanceId { get; private set; }
    public Guid SyncRunId { get; private set; }
    public string SourceEntity { get; private set; }
    public string RawObjectUri { get; private set; }
    public string CorrelationId { get; private set; }
    public string FailedRule { get; private set; }
    public string Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private CanonicalQuarantineEntry(
        Guid id,
        Guid tenantId,
        Guid connectorInstanceId,
        Guid syncRunId,
        string sourceEntity,
        string rawObjectUri,
        string correlationId,
        string failedRule,
        string reason)
        : base(id)
    {
        TenantId = tenantId;
        ConnectorInstanceId = connectorInstanceId;
        SyncRunId = syncRunId;
        SourceEntity = sourceEntity;
        RawObjectUri = rawObjectUri;
        CorrelationId = correlationId;
        FailedRule = failedRule;
        Reason = reason;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private CanonicalQuarantineEntry()
    {
        SourceEntity = string.Empty;
        RawObjectUri = string.Empty;
        CorrelationId = string.Empty;
        FailedRule = string.Empty;
        Reason = string.Empty;
    }

    public static CanonicalQuarantineEntry Create(
        Guid tenantId,
        Guid connectorInstanceId,
        Guid syncRunId,
        string sourceEntity,
        string rawObjectUri,
        string correlationId,
        string failedRule,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntity);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawObjectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(failedRule);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        return new CanonicalQuarantineEntry(Guid.NewGuid(), tenantId, connectorInstanceId, syncRunId, sourceEntity, rawObjectUri, correlationId, failedRule, reason);
    }

    /// <summary>Operador corrigiu o mapeamento e reprocessou a carga (docs/04 §8.2) — marca esta
    /// entrada como resolvida sem apagá-la, preservando o histórico.</summary>
    public void MarkResolved(DateTimeOffset resolvedAt) => ResolvedAt = resolvedAt;
}
