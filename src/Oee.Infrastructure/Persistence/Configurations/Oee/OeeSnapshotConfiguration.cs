using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Oee;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ snapshot OEE bất biến theo WorkUnit và period instance/version.</summary>
/// <remarks>
/// Các tỷ lệ có precision 9,6 và constraint 0..1. Unique index hỗ trợ recalculation idempotent;
/// InputHash/CalculationVersion phân biệt input và phiên bản thuật toán.
/// </remarks>
public sealed class OeeSnapshotConfiguration : IEntityTypeConfiguration<OeeSnapshotEntity>
{
    private const string RatioCheck = "[Availability] BETWEEN 0 AND 1 AND [Performance] BETWEEN 0 AND 1 AND [Quality] BETWEEN 0 AND 1 AND [Oee] BETWEEN 0 AND 1";
    public void Configure(EntityTypeBuilder<OeeSnapshotEntity> b)
    {
        b.ToTable("OeeSnapshots", "oee", t => { t.HasCheckConstraint("CK_OeeSnapshots_Ratios", RatioCheck); t.HasCheckConstraint("CK_OeeSnapshots_Period", "[PeriodEnd] > [PeriodStart]"); t.HasCheckConstraint("CK_OeeSnapshots_Values", "[TotalMinutes] >= 0 AND [UnscheduledMinutes] >= 0 AND [TotalQuantity] >= 0 AND [DefectQuantity] >= 0 AND [GoodQuantity] >= 0"); });
        // Cấu hình đồng nhất bốn tỷ lệ; EF sẽ tạo decimal thay vì float để tránh sai số nhị phân.
        b.ConfigureAudited(); foreach (var property in new[] { nameof(OeeSnapshotEntity.Availability), nameof(OeeSnapshotEntity.Performance), nameof(OeeSnapshotEntity.Quality), nameof(OeeSnapshotEntity.Oee) })
        {
            b.Property(property).HasPrecision(9, 6);
        }
        // Thời lượng và sản lượng dùng decimal(18,4), không dùng SQL time vì đây là duration.
        foreach (var property in new[] { nameof(OeeSnapshotEntity.TotalMinutes), nameof(OeeSnapshotEntity.UnscheduledMinutes), nameof(OeeSnapshotEntity.LoadingMinutes), nameof(OeeSnapshotEntity.AvailabilityLossMinutes), nameof(OeeSnapshotEntity.UtilizationMinutes), nameof(OeeSnapshotEntity.PerformanceLossMinutes), nameof(OeeSnapshotEntity.NetUtilizationMinutes), nameof(OeeSnapshotEntity.QualityLossMinutes), nameof(OeeSnapshotEntity.ValueAddedMinutes), nameof(OeeSnapshotEntity.TotalQuantity), nameof(OeeSnapshotEntity.DefectQuantity), nameof(OeeSnapshotEntity.GoodQuantity) })
        {
            b.Property(property).HasPrecision(18, 4);
        }
        b.Property(x => x.PeriodCode).HasMaxLength(40).IsRequired();
        b.Property(x => x.CalculationVersion).HasMaxLength(40).IsRequired();
        b.Property(x => x.InputHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.AnomalyFlags).HasMaxLength(1000);
        b.HasIndex(x => new { x.WorkUnitId, x.PeriodType, x.PeriodCode, x.PeriodStart, x.PeriodEnd, x.CalculationVersion }).IsUnique();
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
    }
}
