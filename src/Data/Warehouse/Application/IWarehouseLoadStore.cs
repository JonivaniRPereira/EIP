using EIP.Data.Warehouse.Domain;

namespace EIP.Data.Warehouse.Application;

/// <summary>Projeção de <see cref="FactSalesInvoiceItem"/> para o cálculo de métricas certificadas
/// (E6.1) e para o agrupamento dimensional do Analytics Engine (Fase 2, E1.1) — a camada semântica
/// nunca precisa conhecer chaves substitutas, só o necessário para agregação/agrupamento.
/// <see cref="CustomerId"/>/<see cref="ProductId"/> são as chaves de negócio duráveis (não a chave
/// substituta versionada de SCD2) — agrupar por elas nunca separa o mesmo cliente/produto em dois
/// grupos só porque uma versão de dimensão mudou no meio do período. <see cref="CustomerName"/>/
/// <see cref="ProductName"/> refletem a versão de dimensão exatamente referenciada por este item
/// (o nome vigente no momento daquela fatura, não necessariamente o nome atual).</summary>
public sealed record FactSalesInvoiceItemForMetrics(
    Guid SalesInvoiceId,
    string Status,
    decimal Quantity,
    decimal NetAmount,
    int DateKey,
    Guid CustomerId,
    string CustomerName,
    Guid ProductId,
    string ProductName);

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

    /// <summary>Lote de itens de fatura para o cálculo das métricas certificadas (E6.1) e para o
    /// agrupamento dimensional do Analytics Engine (Fase 2, E1.1), filtrado por empresa e/ou período
    /// de negócio (nunca por tenant de outro chamador — <paramref name="tenantId"/> vem sempre do
    /// claim autenticado). O filtro de período compara diretamente a chave determinística de
    /// <see cref="DimDate.DateKey"/>, sem precisar de junção com a dimensão de calendário.</summary>
    Task<IReadOnlyList<FactSalesInvoiceItemForMetrics>> ListFactSalesInvoiceItemsForMetricsAsync(
        Guid tenantId,
        Guid? companyId,
        DateOnly? periodStart,
        DateOnly? periodEnd,
        CancellationToken cancellationToken);
}
