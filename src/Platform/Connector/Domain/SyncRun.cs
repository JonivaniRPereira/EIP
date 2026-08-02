using EIP.BuildingBlocks.DDD;

namespace EIP.Platform.Connector.Domain;

/// <summary>
/// Execução (Sync Job, docs/05-Connector-Framework.md §3/§9) de uma sincronização de
/// <see cref="ConnectorInstance"/>: criada em <see cref="SyncRunStatus.Pending"/> pela API no mesmo
/// momento em que a mensagem é publicada no RabbitMQ, e avançada pelo worker assíncrono. É também o
/// registro de auditoria da execução (contagens, erro, correlação) — não existe uma tabela de
/// auditoria separada para sincronizações na Fase 0.
/// Protegida por RLS obrigatória (ADR-007): pertence sempre a um <see cref="TenantId"/>.
/// </summary>
public sealed class SyncRun : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ConnectorInstanceId { get; private set; }
    public string CorrelationId { get; private set; }
    public SyncRunStatus Status { get; private set; }
    public int? RecordsProcessed { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    private SyncRun(Guid id, Guid tenantId, Guid connectorInstanceId, string correlationId)
        : base(id)
    {
        TenantId = tenantId;
        ConnectorInstanceId = connectorInstanceId;
        CorrelationId = correlationId;
        Status = SyncRunStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private SyncRun()
    {
        CorrelationId = string.Empty;
    }

    public static SyncRun CreatePending(Guid tenantId, Guid connectorInstanceId, string correlationId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        if (connectorInstanceId == Guid.Empty)
        {
            throw new ArgumentException("ConnectorInstanceId é obrigatório.", nameof(connectorInstanceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new SyncRun(Guid.NewGuid(), tenantId, connectorInstanceId, correlationId);
    }

    /// <summary>Transição idempotente: reentregas do worker (at-least-once do RabbitMQ) que chegam
    /// depois de já ter avançado o estado não retrocedem nem duplicam o processamento — só a
    /// primeira entrega efetivamente move o run de Pending para Running.</summary>
    public bool TryStartProcessing()
    {
        if (Status != SyncRunStatus.Pending)
        {
            return false;
        }

        Status = SyncRunStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public void Complete(int recordsProcessed)
    {
        Status = SyncRunStatus.Succeeded;
        RecordsProcessed = recordsProcessed;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = SyncRunStatus.Failed;
        ErrorMessage = errorMessage;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public bool IsTerminal => Status is SyncRunStatus.Succeeded or SyncRunStatus.Failed;
}
