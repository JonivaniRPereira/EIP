namespace EIP.Shared.Contracts.Canonical;

/// <summary>Nomes de entidade canônica reconhecidos pelo Pipeline (docs/roadmap/fase-1-backlog.md
/// E3) — o mesmo valor que um <c>ConnectorInstance.SourceEntity</c> declara. Vive em
/// <c>EIP.Shared</c> porque tanto o módulo Connector (validação leve no registro da instância)
/// quanto o Pipeline (despacho do mapeamento) precisam dele, sem criar uma dependência direta entre
/// os dois módulos.</summary>
public static class CanonicalSourceEntities
{
    public const string Customers = "customers";
    public const string Products = "products";
    public const string SalesInvoices = "sales-invoices";
}
