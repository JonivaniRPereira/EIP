namespace EIP.Data.Canonical.Domain;

/// <summary>
/// Linhagem obrigatória de todo registro canônico (docs/04-Modelo-Canonico.md §4). Identidade de
/// negócio de um registro de origem, conforme §4.1: <c>TenantId + SourceSystemId + SourceEntity +
/// SourceRecordId</c> — precisa ser única, e é o que garante idempotência no pipeline (nunca duplica
/// ao reprocessar o mesmo registro de origem).
/// </summary>
public sealed record CanonicalLineage(
    Guid TenantId,
    Guid CompanyId,
    Guid SourceSystemId,
    string SourceEntity,
    string SourceRecordId,
    DateTimeOffset? SourceUpdatedAt,
    string SchemaVersion,
    string CorrelationId,
    string RawObjectUri);
