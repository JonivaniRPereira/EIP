using EIP.Data.Canonical.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Canonical.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente).</summary>
public sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoices");
        builder.ConfigureCanonicalFields();

        builder.Property(i => i.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Series).HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(i => i.GrossAmount).HasPrecision(19, 4);
        builder.Property(i => i.DiscountAmount).HasPrecision(19, 4);
        builder.Property(i => i.TaxAmount).HasPrecision(19, 4);
        builder.Property(i => i.NetAmount).HasPrecision(19, 4);

        builder.HasIndex(i => i.CustomerId);
    }
}
