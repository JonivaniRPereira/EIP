using EIP.BuildingBlocks.DDD;

namespace EIP.Data.Warehouse.Domain;

/// <summary>
/// Registro de auditoria de uma carga do Warehouse (docs/09-Data-Warehouse.md §7.1, passo 2:
/// "registrar LoadBatch com tenant, origem, intervalo, versão e correlação") — mesmo papel que
/// <c>SyncRun</c> desempenha para uma sincronização de conector, aqui para o processo de
/// materialização de fatos/dimensões a partir do Modelo Canônico já validado. Protegida por RLS
/// obrigatória (ADR-007).
/// </summary>
public sealed class LoadBatch : Entity<Guid>
{
    public Guid TenantId { get; private set; }

    public Guid SourceSystemId { get; private set; }

    public string CorrelationId { get; private set; }

    public LoadBatchStatus Status { get; private set; }

    public int? ItemsConsideredCount { get; private set; }

    public int? FactRowsUpsertedCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    private LoadBatch(Guid id, Guid tenantId, Guid sourceSystemId, string correlationId)
        : base(id)
    {
        TenantId = tenantId;
        SourceSystemId = sourceSystemId;
        CorrelationId = correlationId;
        Status = LoadBatchStatus.Running;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private LoadBatch()
    {
        CorrelationId = string.Empty;
    }

    public static LoadBatch Start(Guid tenantId, Guid sourceSystemId, string correlationId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        if (sourceSystemId == Guid.Empty)
        {
            throw new ArgumentException("SourceSystemId é obrigatório.", nameof(sourceSystemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new LoadBatch(Guid.NewGuid(), tenantId, sourceSystemId, correlationId);
    }

    public void Complete(int itemsConsideredCount, int factRowsUpsertedCount)
    {
        Status = LoadBatchStatus.Succeeded;
        ItemsConsideredCount = itemsConsideredCount;
        FactRowsUpsertedCount = factRowsUpsertedCount;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = LoadBatchStatus.Failed;
        ErrorMessage = errorMessage;
        FinishedAt = DateTimeOffset.UtcNow;
    }
}
