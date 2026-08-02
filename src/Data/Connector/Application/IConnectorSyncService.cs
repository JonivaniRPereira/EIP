using EIP.Data.Connector.Domain;

namespace EIP.Data.Connector.Application;

public sealed record SyncRunRequestResult(bool Success, Guid? SyncRunId, string? Error)
{
    public static SyncRunRequestResult Ok(Guid syncRunId) => new(true, syncRunId, null);

    public static SyncRunRequestResult Failed(string error) => new(false, null, error);
}

/// <summary>Dispara uma sincronização (E7.1/E7.2): valida a instância, cria o <c>SyncRun</c> em
/// <c>Pending</c> e publica a mensagem assíncrona — nunca processa a sincronização de forma síncrona
/// dentro do request HTTP (docs/05-Connector-Framework.md §2: "execução assíncrona").</summary>
public interface IConnectorSyncService
{
    /// <summary>Registra a instância do conector de referência (REST genérico) para o tenant
    /// autenticado. Substitui, na Fase 0, o Connector Registry completo de
    /// docs/05-Connector-Framework.md §4 (Draft/Configuring/Validating ficam para a Fase 1) — aqui
    /// só existe o necessário para ter algo a sincronizar. <paramref name="sourceEntity"/> declara
    /// qual entidade canônica esta instância sincroniza (docs/roadmap/fase-1-backlog.md E3).</summary>
    Task<Guid> RegisterInstanceAsync(Guid tenantId, Guid companyId, string name, string baseUrl, string sourceEntity, CancellationToken cancellationToken);

    Task<SyncRunRequestResult> RequestSyncAsync(Guid connectorInstanceId, Guid tenantId, string correlationId, CancellationToken cancellationToken);

    /// <summary>Consulta o relatório de execução (docs/05 §9) — o resultado auditável da
    /// sincronização exigido por E7.2.</summary>
    Task<SyncRun?> GetRunAsync(Guid syncRunId, Guid tenantId, CancellationToken cancellationToken);
}
