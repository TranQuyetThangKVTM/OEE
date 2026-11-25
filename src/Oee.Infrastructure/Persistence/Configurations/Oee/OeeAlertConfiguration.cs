using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Oee;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ cảnh báo phát sinh từ snapshot + metric và target được chọn.</summary>
public sealed class OeeAlertConfiguration : IEntityTypeConfiguration<OeeAlertEntity>
{
    public void Configure(EntityTypeBuilder<OeeAlertEntity> b)
    {
        b.ToTable("OeeAlerts", "oee", t => t.HasCheckConstraint("CK_OeeAlerts_Ratios", "[ActualValue] BETWEEN 0 AND 1 AND [TargetValue] BETWEEN 0 AND 1"));
        b.ConfigureAudited();
        b.Property(x => x.ActualValue).HasPrecision(9, 6);
        b.Property(x => x.TargetValue).HasPrecision(9, 6);
        b.Property(x => x.AcknowledgedBy).HasMaxLength(120);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.State, x.Severity, x.TriggeredAt });
        b.HasIndex(x => new { x.SnapshotId, x.Metric }).IsUnique();
        b.HasOne(x => x.Snapshot).WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Target).WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.NoAction);
    }
}
