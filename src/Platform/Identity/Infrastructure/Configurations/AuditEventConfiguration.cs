using EIP.Platform.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Platform.Identity.Infrastructure.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.Detail).HasMaxLength(1000);

        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => e.UserId);
    }
}
