namespace EIP.Data.DataLake;

/// <summary>
/// Linhagem obrigatória de todo objeto bruto do Data Lake (docs/04-Modelo-Canonico.md §4,
/// docs/09-Data-Warehouse.md §3.1 — zona "Raw / Data Lake"). <see cref="TenantId"/> nunca deve vir
/// de input de cliente não validado — o mesmo princípio do <c>ITenantContextAccessor</c> usado pela
/// RLS do SQL Server (ADR-007), só que aqui o Object Storage não tem um mecanismo de
/// <c>SESSION_CONTEXT</c> equivalente, então o isolamento é aplicado em código
/// (<see cref="IRawObjectStore"/> constrói/valida a chave sempre a partir do <see cref="TenantId"/>,
/// nunca aceita uma chave arbitrária do chamador).
/// </summary>
public sealed record RawObjectMetadata(
    Guid TenantId,
    Guid SourceSystemId,
    string SourceEntity,
    Guid? ConnectorInstanceId,
    Guid? SyncRunId,
    DateTimeOffset IngestedAt);

/// <summary>Resultado de uma gravação: a chave real (determinística, prefixada por tenant) e o
/// checksum SHA-256 do conteúdo — usado para provar integridade na leitura e para linhagem
/// (docs/04 §4: campo <c>RawObjectUri</c> do registro canônico referencia esta chave).</summary>
public sealed record StoredRawObject(string Key, string Sha256Checksum, long SizeBytes);

/// <summary>
/// Abstração do Data Lake bruto (zona "Raw", docs/09-Data-Warehouse.md §3.1). A implementação
/// concreta (S3-compatible/MinIO) fica em <c>EIP.Data.DataLake.Infrastructure</c> — esta interface
/// não conhece nenhum SDK de storage, para que módulos de Application (ex. o pipeline de
/// sincronização) dependam só do contrato, nunca do cliente S3 concreto.
/// </summary>
public interface IRawObjectStore
{
    /// <summary>Grava o conteúdo bruto; a chave é sempre derivada de <paramref name="metadata"/>
    /// (nunca fornecida pelo chamador) — garante que todo objeto nasce com o prefixo de tenant
    /// correto.</summary>
    Task<StoredRawObject> PutAsync(RawObjectMetadata metadata, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Lê um objeto por chave; <paramref name="tenantId"/> vem sempre do contexto
    /// autenticado (nunca de input de cliente não validado) e é conferido contra o prefixo da
    /// própria chave antes de qualquer leitura — uma chave de outro tenant nunca é aberta, mesmo que
    /// seja fornecida explicitamente.</summary>
    Task<Stream> GetAsync(Guid tenantId, string key, CancellationToken cancellationToken);

    /// <summary>Lista as chaves pertencentes a um tenant — usa o prefixo de chave como filtro nativo
    /// do Object Storage, não uma checagem posterior: fisicamente não é possível uma chave de outro
    /// tenant aparecer no resultado.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(Guid tenantId, CancellationToken cancellationToken);
}
