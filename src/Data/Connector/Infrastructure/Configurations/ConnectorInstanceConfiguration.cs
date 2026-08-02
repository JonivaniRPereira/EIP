using EIP.Data.Connector.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Connector.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente).</summary>
public sealed class ConnectorInstanceConfiguration : IEntityTypeConfiguration<ConnectorInstance>
{
    public void Configure(EntityTypeBuilder<ConnectorInstance> builder)
    {
        builder.ToTable("ConnectorInstances");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.BaseUrl).HasMaxLength(2000).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(i => i.TenantId);
    }
}
