namespace EIP.Data.Semantic.Application;

/// <summary>
/// Metadados obrigatórios de uma métrica certificada (docs/09-Data-Warehouse.md §9: "nenhuma
/// métrica é oficial sem definição, dono, versão e teste de reconciliação"). A definição vive em
/// código nesta fase — um motor de métricas configurável/persistido é explicitamente Fase 2
/// (docs/roadmap/fase-1-backlog.md §6).
/// </summary>
public sealed record CertifiedMetricDefinition(string Name, string Description, string Owner, string Version);

/// <summary>As 3 métricas certificadas de exemplo da fatia Comercial (docs/09 §9). Cada uma tem um
/// teste de reconciliação próprio — ver `EIP.Data.Semantic.IntegrationTests`.</summary>
public static class CertifiedMetrics
{
    public static readonly CertifiedMetricDefinition NetRevenue = new(
        Name: "net_revenue",
        Description: "Receita Líquida: soma de NetAmount em FactSalesInvoiceItem, excluindo documentos cancelados.",
        Owner: "Comercial",
        Version: "1.0");

    public static readonly CertifiedMetricDefinition InvoicedQuantity = new(
        Name: "invoiced_quantity",
        Description: "Quantidade Faturada: soma de quantidade dos itens de fatura válidos (não cancelados).",
        Owner: "Comercial",
        Version: "1.0");

    public static readonly CertifiedMetricDefinition AverageTicket = new(
        Name: "average_ticket",
        Description: "Ticket Médio: Receita Líquida dividida pela quantidade distinta de faturas válidas.",
        Owner: "Comercial",
        Version: "1.0");
}
