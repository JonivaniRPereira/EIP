namespace EIP.Data.Pipeline;

/// <summary>Entrada do Pipeline (E3.3): o conteúdo bruto já extraído pelo conector (nunca lido de
/// volta do Data Lake nesta mesma passada — o worker já tem os bytes em memória logo após a
/// extração; o Data Lake serve para auditoria/reprocessamento futuro, não como intermediário
/// obrigatório de cada sincronização).</summary>
public sealed record PipelineProcessingRequest(
    Guid TenantId,
    Guid CompanyId,
    Guid SourceSystemId,
    Guid SyncRunId,
    string SourceEntity,
    string CorrelationId,
    string RawObjectUri,
    ReadOnlyMemory<byte> RawContent);

/// <summary>Relatório mínimo de execução (docs/04-Modelo-Canonico.md §8.3) — contagens completas
/// (atualizado/rejeitado separadamente, etc.) ficam para E4.1.</summary>
public sealed record PipelineProcessingResult(int ExtractedCount, int AcceptedCount, int RejectedCount);
