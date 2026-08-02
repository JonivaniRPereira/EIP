namespace EIP.Data.Canonical.Domain;

/// <summary>Vocabulário canônico mínimo para a fatia Comercial (docs/04-Modelo-Canonico.md §5.3,
/// §6.2). <see cref="Unknown"/> existe especificamente para quando o pipeline não conseguir mapear
/// um status de origem para nenhum valor conhecido — nunca convertido silenciosamente para
/// <see cref="Issued"/> (docs/04 §6.2: "status desconhecido não pode virar Active/Paid/Completed
/// automaticamente").</summary>
public enum SalesInvoiceStatus
{
    Unknown,
    Issued,
    Canceled,
}
