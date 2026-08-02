using EIP.Data.Canonical.Domain;

namespace EIP.Data.Canonical.Application;

/// <summary>
/// Acesso ao Modelo Canônico da fatia Comercial, consumido pelo Pipeline (E3) para resolução de
/// referências (docs/04-Modelo-Canonico.md §6.3) e persistência idempotente (upsert pela chave de
/// negócio, E3.4). A implementação (Infrastructure) abre uma conexão nova por operação via
/// <c>IDbContextFactory</c> — mesmo padrão do <c>ConnectorSyncStore</c>/<c>MembershipDirectory</c>.
/// </summary>
public interface ICanonicalRecordStore
{
    Task<Customer?> FindCustomerByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    /// <summary>Cria ou atualiza pela chave de negócio (docs/04 §4.1) — nunca duplica ao
    /// reprocessar o mesmo registro de origem. Retorna <see langword="true"/> quando o registro já
    /// existia e foi atualizado (docs/04 §8.3: contagem "atualizadas" separada de "aceitas").</summary>
    Task<bool> UpsertCustomerAsync(Customer candidate, CancellationToken cancellationToken);

    Task<Product?> FindProductByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    Task<bool> UpsertProductAsync(Product candidate, CancellationToken cancellationToken);

    /// <summary>Cria ou atualiza a fatura pela chave de negócio, e substitui integralmente os itens
    /// (delete + insert) — mais simples e robusto do que tentar casar itens antigos com novos por
    /// número de linha, que pode mudar entre sincronizações. Retorna <see langword="true"/> quando a
    /// fatura já existia e foi atualizada.</summary>
    Task<bool> UpsertSalesInvoiceAsync(SalesInvoice invoice, IReadOnlyList<SalesInvoiceItem> items, CancellationToken cancellationToken);

    Task AddQuarantineEntryAsync(CanonicalQuarantineEntry entry, CancellationToken cancellationToken);

    /// <summary>Lista entradas de quarentena do tenant (docs/04 §8.2), mais recentes primeiro,
    /// opcionalmente filtradas por conector e/ou período — usado pelo operador para localizar o que
    /// precisa de correção (E4.2).</summary>
    Task<IReadOnlyList<CanonicalQuarantineEntry>> ListQuarantineEntriesAsync(
        Guid tenantId,
        Guid? connectorInstanceId,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        CancellationToken cancellationToken);

    Task<CanonicalQuarantineEntry?> FindQuarantineEntryAsync(Guid tenantId, Guid quarantineEntryId, CancellationToken cancellationToken);

    /// <summary>Marca como resolvida sem apagar a entrada (docs/04 §8.2: "mantendo auditoria") — usado
    /// depois que um reprocessamento é disparado para ela (E4.2).</summary>
    Task MarkQuarantineEntryResolvedAsync(Guid quarantineEntryId, DateTimeOffset resolvedAt, CancellationToken cancellationToken);

    /// <summary>Estado atual persistido para a fatia Comercial de um conector — contagem de faturas e
    /// soma de <c>NetAmount</c> — usado pela reconciliação Canônico↔Origem (docs/04 §8.3, E4.3).</summary>
    Task<(int Count, decimal NetAmountTotal)> GetSalesInvoiceTotalsAsync(Guid tenantId, Guid sourceSystemId, CancellationToken cancellationToken);

    /// <summary>Mesma ideia de <see cref="GetSalesInvoiceTotalsAsync"/>, mas no grão de item (não de
    /// cabeçalho da fatura) — usado pela reconciliação Canônico↔Fato (docs/09-Data-Warehouse.md §8.2,
    /// E5.4), já que <c>FactSalesInvoiceItem</c> tem uma linha por item, não por fatura.</summary>
    Task<(int Count, decimal NetAmountTotal)> GetSalesInvoiceItemTotalsAsync(Guid tenantId, Guid sourceSystemId, CancellationToken cancellationToken);

    /// <summary>Lote válido pronto para a carga do Data Warehouse (E5.3, docs/09 §7.1, passo 1) — um
    /// item por linha, já com a fatura e as entidades de referência resolvidas. Reflete o estado
    /// atual do Canônico (não um delta desde a última carga — nenhuma incrementalidade ainda,
    /// mesma simplificação já aceita em outros pontos desta fase).</summary>
    Task<IReadOnlyList<SalesInvoiceItemForLoad>> ListSalesInvoiceItemsForLoadAsync(Guid tenantId, Guid sourceSystemId, CancellationToken cancellationToken);
}
