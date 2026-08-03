using EIP.Data.Canonical.Application;
using EIP.Data.Warehouse.Domain;
using EIP.Shared.Contracts.Tenancy;

namespace EIP.Data.Warehouse.Application;

/// <summary>
/// Implementação de referência da carga do Warehouse para a fatia Comercial (E5.3). Processa cada
/// item de fatura isoladamente: uma falha ao resolver uma referência de negócio (tenant/empresa
/// inexistente) é um erro de invariante, não uma condição de quarentena — propaga e falha o
/// <see cref="LoadBatch"/> inteiro, ao contrário do Pipeline (E3), que quarentena por registro.
/// </summary>
public sealed class WarehouseLoadService : IWarehouseLoadService
{
    private readonly ICanonicalRecordStore _canonicalRecordStore;
    private readonly IWarehouseLoadStore _warehouseLoadStore;
    private readonly ITenantDirectory _tenantDirectory;

    public WarehouseLoadService(ICanonicalRecordStore canonicalRecordStore, IWarehouseLoadStore warehouseLoadStore, ITenantDirectory tenantDirectory)
    {
        _canonicalRecordStore = canonicalRecordStore;
        _warehouseLoadStore = warehouseLoadStore;
        _tenantDirectory = tenantDirectory;
    }

    public async Task<WarehouseLoadResult> LoadSalesInvoiceItemsAsync(Guid tenantId, Guid sourceSystemId, string correlationId, CancellationToken cancellationToken)
    {
        var batch = LoadBatch.Start(tenantId, sourceSystemId, correlationId);

        try
        {
            var items = await _canonicalRecordStore.ListSalesInvoiceItemsForLoadAsync(tenantId, sourceSystemId, cancellationToken);

            var tenant = await _tenantDirectory.GetTenantAsync(tenantId, cancellationToken)
                ?? throw new InvalidOperationException($"Tenant '{tenantId}' não encontrado — impossível carregar o Warehouse sem o registro do tenant.");
            var tenantKey = await _warehouseLoadStore.UpsertDimTenantAsync(tenant.TenantId, tenant.Name, cancellationToken);

            var companyKeysByCompanyId = new Dictionary<Guid, int>();
            var factRowsUpserted = 0;

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!companyKeysByCompanyId.TryGetValue(item.CompanyId, out var companyKey))
                {
                    var company = await _tenantDirectory.GetCompanyAsync(tenantId, item.CompanyId, cancellationToken)
                        ?? throw new InvalidOperationException($"Empresa '{item.CompanyId}' não encontrada para o tenant '{tenantId}'.");
                    companyKey = await _warehouseLoadStore.UpsertDimCompanyAsync(tenantId, company.CompanyId, company.Name, company.CountryCode, company.DefaultCurrency, cancellationToken);
                    companyKeysByCompanyId[item.CompanyId] = companyKey;
                }

                var dateKey = await _warehouseLoadStore.EnsureDimDateAsync(item.IssueDate, cancellationToken);

                // Sem uma fonte real de metadados de moeda ainda — o nome descritivo fica igual ao
                // código até que uma exista (docs/roadmap/fase-1-backlog.md E5.1: "mínima").
                var currencyKey = await _warehouseLoadStore.EnsureDimCurrencyAsync(item.CurrencyCode, item.CurrencyCode, cancellationToken);

                await _warehouseLoadStore.UpsertCurrentDimCustomerVersionAsync(
                    tenantId, item.CustomerId, item.CustomerCode, item.CustomerName, email: null,
                    item.CustomerCity, item.CustomerStateOrRegion, item.CustomerCountryCode, item.CustomerIsActive, cancellationToken);
                var customerKey = await _warehouseLoadStore.ResolveDimCustomerKeyAsOfAsync(tenantId, item.CustomerId, item.IssueDate, cancellationToken);

                await _warehouseLoadStore.UpsertCurrentDimProductVersionAsync(
                    tenantId, item.ProductId, item.ProductCode, item.ProductName, item.ProductType.ToString(),
                    item.ProductUnitOfMeasure, item.ProductIsActive, cancellationToken);
                var productKey = await _warehouseLoadStore.ResolveDimProductKeyAsOfAsync(tenantId, item.ProductId, item.IssueDate, cancellationToken);

                var fact = FactSalesInvoiceItem.Create(
                    tenantId,
                    tenantKey,
                    companyKey,
                    dateKey,
                    customerKey,
                    productKey,
                    currencyKey,
                    item.SourceSystemId,
                    item.SourceEntity,
                    item.SourceRecordId,
                    item.SalesInvoiceId,
                    item.SalesInvoiceItemId,
                    item.RawObjectUri,
                    batch.Id,
                    item.InvoiceNumber,
                    item.Status.ToString(),
                    item.LineNumber,
                    item.Quantity,
                    item.GrossAmount,
                    item.DiscountAmount,
                    item.TaxAmount,
                    item.NetAmount);

                await _warehouseLoadStore.UpsertFactSalesInvoiceItemAsync(fact, cancellationToken);
                factRowsUpserted++;
            }

            batch.Complete(items.Count, factRowsUpserted);
            await _warehouseLoadStore.SaveLoadBatchAsync(batch, cancellationToken);

            return new WarehouseLoadResult(items.Count, factRowsUpserted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            batch.Fail(ex.Message);
            await _warehouseLoadStore.SaveLoadBatchAsync(batch, cancellationToken);
            throw;
        }
    }
}
