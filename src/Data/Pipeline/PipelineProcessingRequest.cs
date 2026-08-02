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

/// <summary>
/// Relatório de execução (docs/04-Modelo-Canonico.md §8.3: "extraídas, aceitas, atualizadas,
/// excluídas, rejeitadas e processadas"). Neste pipeline de referência <see cref="ExtractedCount"/>
/// e "processadas" coincidem sempre — todo registro extraído é totalmente processado na mesma
/// passada síncrona, não há estado parcial/retomável ainda. <see cref="DeletedCount"/> é sempre 0: o
/// conector de referência (REST genérico, dados fixos) não emite sinal de exclusão de origem.
/// <see cref="NetAmountTotal"/> só é preenchido para `sales-invoices` (soma de <c>NetAmount</c> dos
/// registros aceitos nesta execução) — usado pela reconciliação Canônico↔Origem (E4.3).
/// </summary>
public sealed record PipelineProcessingResult(
    int ExtractedCount,
    int AcceptedCount,
    int UpdatedCount,
    int RejectedCount,
    int DeletedCount = 0,
    decimal? NetAmountTotal = null);
