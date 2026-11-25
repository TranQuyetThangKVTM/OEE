using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Downtime;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ DowntimeEvent có EndAt nullable khi máy đang dừng.</summary>
/// <remarks>
/// Filtered unique index trên WorkUnitId khi EndAt IS NULL bảo vệ chỉ một event mở cho mỗi máy,
/// kể cả khi hai Worker/request cùng lưu. UpdatedAt là concurrency token cho close/reclassify.
/// </remarks>
public sealed class DowntimeEventConfiguration : IEntityTypeConfiguration<DowntimeEventEntity>
{
    public void Configure(EntityTypeBuilder<DowntimeEventEntity> b)
    {
        b.ToTable("DowntimeEvents", "downtime", t => t.HasCheckConstraint("CK_DowntimeEvents_Period", "[EndAt] IS NULL OR [EndAt] > [StartAt]"));
        b.ConfigureAudited();
        b.Property(x => x.UpdatedAt).IsConcurrencyToken();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => x.WorkUnitId).IsUnique().HasFilter("[EndAt] IS NULL");
        b.HasIndex(x => new { x.WorkUnitId, x.StartAt, x.EndAt });
        b.HasIndex(x => new { x.DowntimeReasonId, x.StartAt });
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ProductionOutput).WithMany().HasForeignKey(x => x.ProductionOutputId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.DowntimeReason).WithMany().HasForeignKey(x => x.DowntimeReasonId).OnDelete(DeleteBehavior.NoAction);
    }
}
