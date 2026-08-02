namespace EIP.Data.Warehouse.Application;

/// <summary>Contagens do lote (docs/09-Data-Warehouse.md §7.1, passo 2) — quantos itens canônicos
/// foram considerados e quantas linhas de fato foram de fato criadas/atualizadas.</summary>
public sealed record WarehouseLoadResult(int ItemsConsideredCount, int FactRowsUpsertedCount);

/// <summary>
/// Processo de carga do Data Warehouse (E5.3, docs/09 §7.1): recebe o lote já validado do Modelo
/// Canônico — nunca direto da origem — resolve/atualiza dimensões (aplicando SCD Tipo 2 quando
/// necessário) e materializa <c>FactSalesInvoiceItem</c>. Disparado depois que uma sincronização de
/// `sales-invoices` termina (E3/E4), nunca antes.
/// </summary>
public interface IWarehouseLoadService
{
    Task<WarehouseLoadResult> LoadSalesInvoiceItemsAsync(Guid tenantId, Guid sourceSystemId, string correlationId, CancellationToken cancellationToken);
}
