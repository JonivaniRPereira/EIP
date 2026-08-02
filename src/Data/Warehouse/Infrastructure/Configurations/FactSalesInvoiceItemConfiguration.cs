using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007). Grão imutável (docs/09 §5.3): uma linha por item
/// de fatura, chave de negócio <c>(TenantId, SourceSystemId, SourceEntity, SourceRecordId)</c> — ver
/// <see cref="FactSalesInvoiceItem"/>.</summary>
public sealed class FactSalesInvoiceItemConfiguration : IEntityTypeConfiguration<FactSalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<FactSalesInvoiceItem> builder)
    {
        builder.ToTable("FactSalesInvoiceItem");
        builder.HasKey(f => f.FactSalesInvoiceItemKey);
        builder.Property(f => f.FactSalesInvoiceItemKey).ValueGeneratedOnAdd();

        builder.Property(f => f.TenantId).IsRequired();
        builder.Property(f => f.SourceSystemId).IsRequired();
        builder.Property(f => f.SourceEntity).HasMaxLength(200).IsRequired();
        builder.Property(f => f.SourceRecordId).HasMaxLength(200).IsRequired();
        builder.Property(f => f.RawObjectUri).HasMaxLength(2000).IsRequired();
        builder.Property(f => f.InvoiceNumber).HasMaxLength(50).IsRequired();

        builder.Property(f => f.Quantity).HasPrecision(19, 4);
        builder.Property(f => f.GrossAmount).HasPrecision(19, 4);
        builder.Property(f => f.DiscountAmount).HasPrecision(19, 4);
        builder.Property(f => f.TaxAmount).HasPrecision(19, 4);
        builder.Property(f => f.NetAmount).HasPrecision(19, 4);

        builder.HasIndex(f => f.TenantId);
        builder.HasIndex(f => f.DateKey);
        builder.HasIndex(f => f.CustomerKey);
        builder.HasIndex(f => f.ProductKey);
        builder.HasIndex(f => f.LoadBatchId);
        builder.HasIndex(f => new { f.TenantId, f.SourceSystemId, f.SourceEntity, f.SourceRecordId }).IsUnique();
    }
}
