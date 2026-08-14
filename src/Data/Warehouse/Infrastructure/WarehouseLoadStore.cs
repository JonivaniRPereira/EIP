using EIP.Data.Warehouse.Application;
using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Warehouse.Infrastructure;

public sealed class WarehouseLoadStore : IWarehouseLoadStore
{
    private readonly IDbContextFactory<WarehouseDbContext> _dbContextFactory;

    public WarehouseLoadStore(IDbContextFactory<WarehouseDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<int> UpsertDimTenantAsync(Guid tenantId, string name, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DimTenants.SingleOrDefaultAsync(d => d.TenantId == tenantId, cancellationToken);

        if (existing is null)
        {
            existing = DimTenant.Create(tenantId, name);
            db.DimTenants.Add(existing);
        }
        else
        {
            existing.ApplyUpdate(name);
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing.TenantKey;
    }

    public async Task<int> UpsertDimCompanyAsync(Guid tenantId, Guid companyId, string name, string countryCode, string defaultCurrency, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DimCompanies.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.CompanyId == companyId, cancellationToken);

        if (existing is null)
        {
            existing = DimCompany.Create(tenantId, companyId, name, countryCode, defaultCurrency);
            db.DimCompanies.Add(existing);
        }
        else
        {
            existing.ApplyUpdate(name, countryCode, defaultCurrency);
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing.CompanyKey;
    }

    public async Task<int> EnsureDimDateAsync(DateOnly calendarDate, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dateKey = DimDate.ToDateKey(calendarDate);
        var existing = await db.DimDates.SingleOrDefaultAsync(d => d.DateKey == dateKey, cancellationToken);

        if (existing is not null)
        {
            return existing.DateKey;
        }

        var created = DimDate.FromDate(calendarDate);
        db.DimDates.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created.DateKey;
    }

    public async Task<int> EnsureDimCurrencyAsync(string code, string name, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DimCurrencies.SingleOrDefaultAsync(d => d.Code == code, cancellationToken);

        if (existing is not null)
        {
            return existing.CurrencyKey;
        }

        var created = DimCurrency.Create(code, name);
        db.DimCurrencies.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created.CurrencyKey;
    }

    public async Task UpsertCurrentDimCustomerVersionAsync(
        Guid tenantId,
        Guid customerId,
        string code,
        string name,
        string? email,
        string? city,
        string? stateOrRegion,
        string? countryCode,
        bool isActive,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var current = await db.DimCustomers.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.CustomerId == customerId && d.IsCurrent, cancellationToken);

        if (current is null)
        {
            db.DimCustomers.Add(DimCustomer.CreateCurrentVersion(
                tenantId, customerId, code, name, email, city, stateOrRegion, countryCode, isActive, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!current.HasDescriptiveChangeComparedTo(name, email, city, stateOrRegion, countryCode, isActive))
        {
            return;
        }

        // SCD Tipo 2 (docs/09 §6.1): nunca sobrescreve — fecha a versão atual e abre uma nova.
        var now = DateTimeOffset.UtcNow;
        current.Expire(now);
        db.DimCustomers.Add(DimCustomer.CreateCurrentVersion(
            tenantId, customerId, code, name, email, city, stateOrRegion, countryCode, isActive, now));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ResolveDimCustomerKeyAsOfAsync(Guid tenantId, Guid customerId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var asOf = ToUtcMidnight(asOfDate);

        var versionAsOf = await db.DimCustomers
            .Where(d => d.TenantId == tenantId && d.CustomerId == customerId && d.EffectiveFrom <= asOf && (d.EffectiveTo == null || asOf < d.EffectiveTo))
            .SingleOrDefaultAsync(cancellationToken);

        if (versionAsOf is not null)
        {
            return versionAsOf.CustomerKey;
        }

        // A origem ainda não fornece SourceUpdatedAt, então a primeira versão nasce datada do
        // momento da carga, não do negócio — uma fatura com data anterior à primeira carga cai aqui.
        var earliest = await db.DimCustomers
            .Where(d => d.TenantId == tenantId && d.CustomerId == customerId)
            .OrderBy(d => d.EffectiveFrom)
            .FirstAsync(cancellationToken);

        return earliest.CustomerKey;
    }

    public async Task UpsertCurrentDimProductVersionAsync(
        Guid tenantId,
        Guid productId,
        string code,
        string name,
        string productType,
        string? unitOfMeasure,
        bool isActive,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var current = await db.DimProducts.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.ProductId == productId && d.IsCurrent, cancellationToken);

        if (current is null)
        {
            db.DimProducts.Add(DimProduct.CreateCurrentVersion(
                tenantId, productId, code, name, productType, categoryKey: null, unitOfMeasure, isActive, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!current.HasDescriptiveChangeComparedTo(name, productType, current.CategoryKey, unitOfMeasure, isActive))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        current.Expire(now);
        db.DimProducts.Add(DimProduct.CreateCurrentVersion(
            tenantId, productId, code, name, productType, categoryKey: null, unitOfMeasure, isActive, now));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ResolveDimProductKeyAsOfAsync(Guid tenantId, Guid productId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var asOf = ToUtcMidnight(asOfDate);

        var versionAsOf = await db.DimProducts
            .Where(d => d.TenantId == tenantId && d.ProductId == productId && d.EffectiveFrom <= asOf && (d.EffectiveTo == null || asOf < d.EffectiveTo))
            .SingleOrDefaultAsync(cancellationToken);

        if (versionAsOf is not null)
        {
            return versionAsOf.ProductKey;
        }

        var earliest = await db.DimProducts
            .Where(d => d.TenantId == tenantId && d.ProductId == productId)
            .OrderBy(d => d.EffectiveFrom)
            .FirstAsync(cancellationToken);

        return earliest.ProductKey;
    }

    public async Task<bool> UpsertFactSalesInvoiceItemAsync(FactSalesInvoiceItem candidate, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.FactSalesInvoiceItems.SingleOrDefaultAsync(
            f => f.TenantId == candidate.TenantId
                && f.SourceSystemId == candidate.SourceSystemId
                && f.SourceEntity == candidate.SourceEntity
                && f.SourceRecordId == candidate.SourceRecordId,
            cancellationToken);

        if (existing is null)
        {
            db.FactSalesInvoiceItems.Add(candidate);
        }
        else
        {
            existing.ApplyUpdate(
                candidate.TenantKey,
                candidate.CompanyKey,
                candidate.DateKey,
                candidate.CustomerKey,
                candidate.ProductKey,
                candidate.CurrencyKey,
                candidate.SalesInvoiceId,
                candidate.SalesInvoiceItemId,
                candidate.RawObjectUri,
                candidate.LoadBatchId,
                candidate.InvoiceNumber,
                candidate.Status,
                candidate.Quantity,
                candidate.GrossAmount,
                candidate.DiscountAmount,
                candidate.TaxAmount,
                candidate.NetAmount);
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing is not null;
    }

    public async Task SaveLoadBatchAsync(LoadBatch batch, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.LoadBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int Count, decimal NetAmountTotal)> GetFactSalesInvoiceItemTotalsAsync(Guid tenantId, Guid sourceSystemId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var facts = db.FactSalesInvoiceItems.Where(f => f.TenantId == tenantId && f.SourceSystemId == sourceSystemId);

        var count = await facts.CountAsync(cancellationToken);
        var netAmountTotal = count == 0 ? 0m : await facts.SumAsync(f => f.NetAmount, cancellationToken);

        return (count, netAmountTotal);
    }

    public async Task<IReadOnlyList<FactSalesInvoiceItemForMetrics>> ListFactSalesInvoiceItemsForMetricsAsync(
        Guid tenantId,
        Guid? companyId,
        DateOnly? periodStart,
        DateOnly? periodEnd,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.FactSalesInvoiceItems.Where(f => f.TenantId == tenantId);

        if (companyId is { } company)
        {
            var companyKeys = db.DimCompanies.Where(c => c.TenantId == tenantId && c.CompanyId == company).Select(c => c.CompanyKey);
            query = query.Where(f => companyKeys.Contains(f.CompanyKey));
        }

        if (periodStart is { } start)
        {
            var startKey = DimDate.ToDateKey(start);
            query = query.Where(f => f.DateKey >= startKey);
        }

        if (periodEnd is { } end)
        {
            var endKey = DimDate.ToDateKey(end);
            query = query.Where(f => f.DateKey <= endKey);
        }

        return await query
            .Join(db.DimCustomers.Where(c => c.TenantId == tenantId), f => f.CustomerKey, c => c.CustomerKey, (f, c) => new { f, c })
            .Join(db.DimProducts.Where(p => p.TenantId == tenantId), fc => fc.f.ProductKey, p => p.ProductKey, (fc, p) => new { fc.f, fc.c, p })
            .Select(x => new FactSalesInvoiceItemForMetrics(
                x.f.SalesInvoiceId,
                x.f.Status,
                x.f.Quantity,
                x.f.NetAmount,
                x.f.DateKey,
                x.c.CustomerId,
                x.c.Name,
                x.p.ProductId,
                x.p.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastSuccessfulLoadAtAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.LoadBatches
            .Where(b => b.TenantId == tenantId && b.Status == LoadBatchStatus.Succeeded)
            .OrderByDescending(b => b.FinishedAt)
            .Select(b => b.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DateTimeOffset ToUtcMidnight(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
