using EIP.Data.Canonical.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Canonical.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente).</summary>
public sealed class SalesInvoiceItemConfiguration : IEntityTypeConfiguration<SalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        builder.ToTable("SalesInvoiceItems");
        builder.ConfigureCanonicalFields();

        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.Quantity).HasPrecision(19, 4);
        builder.Property(i => i.UnitPrice).HasPrecision(19, 4);
        builder.Property(i => i.DiscountAmount).HasPrecision(19, 4);
        builder.Property(i => i.TaxAmount).HasPrecision(19, 4);
        builder.Property(i => i.GrossAmount).HasPrecision(19, 4);
        builder.Property(i => i.NetAmount).HasPrecision(19, 4);

        builder.HasIndex(i => i.SalesInvoiceId);
        builder.HasIndex(i => i.ProductId);
    }
}
