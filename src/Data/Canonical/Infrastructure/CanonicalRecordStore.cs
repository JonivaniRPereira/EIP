using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Domain;
using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Canonical.Infrastructure;

public sealed class CanonicalRecordStore : ICanonicalRecordStore
{
    private readonly IDbContextFactory<CanonicalDbContext> _dbContextFactory;

    public CanonicalRecordStore(IDbContextFactory<CanonicalDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Customer?> FindCustomerByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Customers.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Code == code, cancellationToken);
    }

    public async Task<bool> UpsertCustomerAsync(Customer candidate, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindByBusinessKeyAsync(db.Customers, candidate, cancellationToken);

        if (existing is null)
        {
            db.Customers.Add(candidate);
        }
        else
        {
            existing.ApplyUpdate(candidate.ToLineage(), candidate.Name, candidate.IsActive, candidate.TaxId, candidate.Email, candidate.City, candidate.StateOrRegion, candidate.CountryCode);
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing is not null;
    }

    public async Task<Product?> FindProductByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Products.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Code == code, cancellationToken);
    }

    public async Task<bool> UpsertProductAsync(Product candidate, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindByBusinessKeyAsync(db.Products, candidate, cancellationToken);

        if (existing is null)
        {
            db.Products.Add(candidate);
        }
        else
        {
            existing.ApplyUpdate(candidate.ToLineage(), candidate.Name, candidate.ProductType, candidate.IsActive, candidate.CategoryId, candidate.UnitOfMeasure);
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing is not null;
    }

    public async Task<bool> UpsertSalesInvoiceAsync(SalesInvoice invoice, IReadOnlyList<SalesInvoiceItem> items, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindByBusinessKeyAsync(db.SalesInvoices, invoice, cancellationToken);

        Guid invoiceId;
        if (existing is null)
        {
            db.SalesInvoices.Add(invoice);
            invoiceId = invoice.Id;
        }
        else
        {
            existing.ApplyUpdate(
                invoice.ToLineage(),
                invoice.IssueDate,
                invoice.CustomerId,
                invoice.Status,
                invoice.CurrencyCode,
                invoice.GrossAmount,
                invoice.DiscountAmount,
                invoice.NetAmount,
                invoice.Series,
                invoice.SalesOrderId,
                invoice.TaxAmount);
            invoiceId = existing.Id;

            // Substitui os itens integralmente (delete + insert) em vez de tentar casar item antigo
            // com novo por número de linha, que pode mudar entre sincronizações.
            var oldItems = await db.SalesInvoiceItems.Where(i => i.SalesInvoiceId == invoiceId).ToListAsync(cancellationToken);
            db.SalesInvoiceItems.RemoveRange(oldItems);
        }

        foreach (var item in items)
        {
            // Os itens chegam com o Id do candidato de fatura (que pode não ser o Id realmente
            // persistido, se a fatura já existia) — reatribuídos aqui antes de gravar.
            item.ReassignInvoice(invoiceId);
            db.SalesInvoiceItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing is not null;
    }

    public async Task AddQuarantineEntryAsync(CanonicalQuarantineEntry entry, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.QuarantineEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CanonicalQuarantineEntry>> ListQuarantineEntriesAsync(
        Guid tenantId,
        Guid? connectorInstanceId,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.QuarantineEntries.Where(q => q.TenantId == tenantId);

        if (connectorInstanceId is { } id)
        {
            query = query.Where(q => q.ConnectorInstanceId == id);
        }

        if (createdFrom is { } fromValue)
        {
            query = query.Where(q => q.CreatedAt >= fromValue);
        }

        if (createdTo is { } toValue)
        {
            query = query.Where(q => q.CreatedAt <= toValue);
        }

        return await query.OrderByDescending(q => q.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<CanonicalQuarantineEntry?> FindQuarantineEntryAsync(Guid tenantId, Guid quarantineEntryId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.QuarantineEntries.SingleOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quarantineEntryId, cancellationToken);
    }

    public async Task MarkQuarantineEntryResolvedAsync(Guid quarantineEntryId, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.QuarantineEntries.SingleOrDefaultAsync(q => q.Id == quarantineEntryId, cancellationToken);
        if (entry is null)
        {
            return;
        }

        entry.MarkResolved(resolvedAt);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int Count, decimal NetAmountTotal)> GetSalesInvoiceTotalsAsync(Guid tenantId, Guid sourceSystemId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var invoices = db.SalesInvoices.Where(i => i.TenantId == tenantId && i.SourceSystemId == sourceSystemId);

        var count = await invoices.CountAsync(cancellationToken);
        var netAmountTotal = count == 0 ? 0m : await invoices.SumAsync(i => i.NetAmount, cancellationToken);

        return (count, netAmountTotal);
    }

    private static Task<TEntity?> FindByBusinessKeyAsync<TEntity>(DbSet<TEntity> set, TEntity candidate, CancellationToken cancellationToken)
        where TEntity : CanonicalEntity
    {
        return set.SingleOrDefaultAsync(
            e => e.TenantId == candidate.TenantId
                && e.SourceSystemId == candidate.SourceSystemId
                && e.SourceEntity == candidate.SourceEntity
                && e.SourceRecordId == candidate.SourceRecordId,
            cancellationToken);
    }
}
