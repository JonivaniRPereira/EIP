namespace EIP.Data.Connector.Application.Abstractions;

/// <summary>Uma mensagem atualmente na DLQ (E2.2 da Fase 3), já traduzida para fora dos detalhes de
/// RabbitMQ (a Application não conhece <c>x-death</c>/delivery tag).</summary>
public sealed record DlqMessage(
    Guid SyncRunId,
    Guid ConnectorInstanceId,
    string CorrelationId,
    string ContractVersion,
    string? FailureReason,
    DateTimeOffset? DeadLetteredAtUtc);

public enum DlqRequeueOutcome
{
    Requeued,
    NotFound,
}

/// <summary>Leitura/reprocessamento da DLQ do conector de referência (E2.2). Escopado por tenant em
/// ambos os métodos — a fila DLQ é um recurso físico único compartilhado por todos os tenants (não há
/// RLS possível em RabbitMQ), então o isolamento tem que ser garantido aqui, filtrando pelo
/// <c>TenantId</c> de cada <see cref="Contracts.SyncRequestedMessage"/> antes de expor qualquer coisa
/// — nunca por uma permissão global de "acesso a toda a DLQ" (não existe conceito de admin de
/// plataforma nesta fase; ver docs/roadmap/fase-3-backlog.md §3 E2). Mesmo padrão IDOR-safe de
/// <c>ConnectorsController.GetSyncRun</c>: uma mensagem de outro tenant é tratada como inexistente,
/// nunca como "existe mas não é seu".</summary>
public interface IConnectorDlqReader
{
    Task<IReadOnlyList<DlqMessage>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<DlqRequeueOutcome> RequeueAsync(Guid tenantId, Guid syncRunId, CancellationToken cancellationToken);
}
