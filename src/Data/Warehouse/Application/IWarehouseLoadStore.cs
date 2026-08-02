using EIP.Data.Warehouse.Domain;

namespace EIP.Data.Warehouse.Application;

/// <summary>
/// Acesso ao Core Dimensional do Warehouse (docs/09-Data-Warehouse.md §3.1), consumido pelo processo
/// de carga (E5.3). A implementação (Infrastructure) abre uma conexão nova por operação via
/// <c>IDbContextFactory</c> — mesmo padrão do <c>ICanonicalRecordStore</c>.
/// </summary>
public interface IWarehouseLoadStore
{
    /// <summary>SCD Tipo 1 (docs/09 §6.1) — cria ou sobrescreve. Retorna <see cref="DimTenant.TenantKey"/>.</summary>
    Task<int> UpsertDimTenantAsync(Guid tenantId, string name, CancellationToken cancellationToken);

    /// <summary>SCD Tipo 1. Retorna <see cref="DimCompany.CompanyKey"/>.</summary>
    Task<int> UpsertDimCompanyAsync(Guid tenantId, Guid companyId, string name, string countryCode, string defaultCurrency, CancellationToken cancellationToken);

    /// <summary>Garante que a linha de calendário exista (dimensão de referência, pré-gerada sob
    /// demanda — nunca duplica); retorna <see cref="DimDate.DateKey"/>.</summary>
    Task<int> EnsureDimDateAsync(DateOnly calendarDate, CancellationToken cancellationToken);

    /// <summary>Garante que a linha de moeda exista; retorna <see cref="DimCurrency.CurrencyKey"/>.</summary>
    Task<int> EnsureDimCurrencyAsync(string code, string name, CancellationToken cancellationToken);

    /// <summary>SCD Tipo 2 (docs/09 §6.1): cria a primeira versão se o cliente nunca foi carregado, ou
    /// fecha a versão atual e abre uma nova quando um atributo descritivo muda
    /// (<see cref="DimCustomer.HasDescriptiveChangeComparedTo"/>) — nunca sobrescreve uma versão
    /// existente.</summary>
    Task UpsertCurrentDimCustomerVersionAsync(
        Guid tenantId,
        Guid customerId,
        string code,
        string name,
        string? email,
        string? city,
        string? stateOrRegion,
        string? countryCode,
        bool isActive,
        CancellationToken cancellationToken);

    /// <summary>Resolve a versão de <see cref="DimCustomer"/> válida para a data de negócio do fato
    /// (docs/09 §6.1: "a tabela de fatos referencia a versão da dimensão válida para a data de
    /// negócio"). Se <paramref name="asOfDate"/> for anterior à primeira versão conhecida (a origem
    /// não fornece <c>SourceUpdatedAt</c> ainda, então a primeira versão nasce datada do momento da
    /// carga, não do negócio), cai de volta para a versão mais antiga disponível — nunca retorna
    /// "não encontrado" para um cliente que já foi carregado ao menos uma vez nesta carga.</summary>
    Task<int> ResolveDimCustomerKeyAsOfAsync(Guid tenantId, Guid customerId, DateOnly asOfDate, CancellationToken cancellationToken);

    Task UpsertCurrentDimProductVersionAsync(
        Guid tenantId,
        Guid productId,
        string code,
        string name,
        string productType,
        string? unitOfMeasure,
        bool isActive,
        CancellationToken cancellationToken);

    Task<int> ResolveDimProductKeyAsOfAsync(Guid tenantId, Guid productId, DateOnly asOfDate, CancellationToken cancellationToken);

    /// <summary>Cria ou atualiza pela chave de negócio
    /// <c>(TenantId, SourceSystemId, SourceEntity, SourceRecordId)</c> — nunca duplica ao recarregar o
    /// mesmo item de origem (docs/04 §4.1, aplicado também ao Warehouse). Retorna
    /// <see langword="true"/> quando já existia.</summary>
    Task<bool> UpsertFactSalesInvoiceItemAsync(FactSalesInvoiceItem candidate, CancellationToken cancellationToken);

    Task SaveLoadBatchAsync(LoadBatch batch, CancellationToken cancellationToken);

    /// <summary>Estado atual persistido no fato — usado pela reconciliação Canônico↔Fato (E5.4).</summary>
    Task<(int Count, decimal NetAmountTotal)> GetFactSalesInvoiceItemTotalsAsync(Guid tenantId, Guid sourceSystemId, CancellationToken cancellationToken);
}
