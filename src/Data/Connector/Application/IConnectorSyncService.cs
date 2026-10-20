using EIP.Data.Connector.Domain;

namespace EIP.Data.Connector.Application;

public sealed record SyncRunRequestResult(bool Success, Guid? SyncRunId, string? Error, TimeSpan? RetryAfter = null)
{
    public static SyncRunRequestResult Ok(Guid syncRunId) => new(true, syncRunId, null);

    public static SyncRunRequestResult Failed(string error) => new(false, null, error);

    public static SyncRunRequestResult QuotaExceeded(TimeSpan retryAfter) =>
        new(false, null, "Limite de sincronizações excedido. Aguarde antes de tentar novamente.", retryAfter);
}

/// <summary>Dispara uma sincronização (E7.1/E7.2): valida a instância, cria o <c>SyncRun</c> em
/// <c>Pending</c> e publica a mensagem assíncrona — nunca processa a sincronização de forma síncrona
/// dentro do request HTTP (docs/05-Connector-Framework.md §2: "execução assíncrona").</summary>
public interface IConnectorSyncService
{
    Task<ConnectorInstance?> UpdateConfigurationAsync(Guid tenantId, Guid connectorInstanceId, string name, string baseUrl, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectorInstance>> ListInstancesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncRun>?> ListRunsAsync(Guid tenantId, Guid connectorInstanceId, int limit, CancellationToken cancellationToken, DateTimeOffset? before = null, Guid? beforeId = null);
    /// <summary>Registra a instância do conector de referência (REST genérico) para o tenant
    /// autenticado. Substitui, na Fase 0, o Connector Registry completo de
    /// docs/05-Connector-Framework.md §4 (Draft/Configuring/Validating ficam para a Fase 1) — aqui
    /// só existe o necessário para ter algo a sincronizar. <paramref name="sourceEntity"/> declara
    /// qual entidade canônica esta instância sincroniza (docs/roadmap/fase-1-backlog.md E3).</summary>
    Task<Guid> RegisterInstanceAsync(Guid tenantId, Guid companyId, string name, string baseUrl, string sourceEntity, CancellationToken cancellationToken);

    /// <summary><paramref name="reprocessFromUtc"/> (E7.2) reconstrói um período específico — quando
    /// informado, ignora o watermark salvo só para esta execução, sem avançá-lo depois (nunca move a
    /// cadência automática, docs/09 §7.2: "reconstruir um intervalo sem apagar dados não
    /// relacionados" — o upsert idempotente do Pipeline/Warehouse já garante isso).</summary>
    Task<SyncRunRequestResult> RequestSyncAsync(Guid connectorInstanceId, Guid tenantId, string correlationId, DateTimeOffset? reprocessFromUtc, CancellationToken cancellationToken);

    /// <summary>Consulta o relatório de execução (docs/05 §9) — o resultado auditável da
    /// sincronização exigido por E7.2.</summary>
    Task<SyncRun?> GetRunAsync(Guid syncRunId, Guid tenantId, CancellationToken cancellationToken);
}
