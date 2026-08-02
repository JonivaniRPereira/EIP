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
    /// reprocessar o mesmo registro de origem.</summary>
    Task UpsertCustomerAsync(Customer candidate, CancellationToken cancellationToken);

    Task<Product?> FindProductByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    Task UpsertProductAsync(Product candidate, CancellationToken cancellationToken);

    /// <summary>Cria ou atualiza a fatura pela chave de negócio, e substitui integralmente os itens
    /// (delete + insert) — mais simples e robusto do que tentar casar itens antigos com novos por
    /// número de linha, que pode mudar entre sincronizações.</summary>
    Task UpsertSalesInvoiceAsync(SalesInvoice invoice, IReadOnlyList<SalesInvoiceItem> items, CancellationToken cancellationToken);

    Task AddQuarantineEntryAsync(CanonicalQuarantineEntry entry, CancellationToken cancellationToken);
}
