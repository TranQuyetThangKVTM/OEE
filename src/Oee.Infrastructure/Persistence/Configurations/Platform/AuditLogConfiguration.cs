using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Platform;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ AuditLog append-only, JSON hợp lệ và index cho các bộ lọc quản trị.</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> b)
    {
        b.ToTable("AuditLogs", "audit", t => t.HasCheckConstraint("CK_AuditLogs_ChangesJson", "[Changes] IS NULL OR ISJSON([Changes]) = 1"));
        b.ConfigureEntity();
        b.Property(x => x.UserId).HasMaxLength(64);
        b.Property(x => x.UserName).HasMaxLength(200);
        b.Property(x => x.Action).HasMaxLength(40).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(256).IsRequired();
        b.Property(x => x.EntitySubject).HasMaxLength(300);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => x.Timestamp);
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Timestamp });
        b.HasIndex(x => new { x.UserId, x.Timestamp });
    }
}
