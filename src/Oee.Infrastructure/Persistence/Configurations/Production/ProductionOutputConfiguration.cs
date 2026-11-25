using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Production;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ fact sản lượng bắt buộc có FK/index tới ProductionPlan.</summary>
/// <remarks>
/// Quantity/speed/duration dùng decimal(18,4). UnscheduledMinutes là duration, không phải giờ trong ngày.
/// Check constraint bảo vệ defect không vượt total và period có độ dài dương.
/// </remarks>
public sealed class ProductionOutputConfiguration : IEntityTypeConfiguration<ProductionOutputEntity>
{
    public void Configure(EntityTypeBuilder<ProductionOutputEntity> b)
    {
        b.ToTable("ProductionOutputs", "production", t => { t.HasCheckConstraint("CK_ProductionOutputs_Period", "[PeriodEnd] > [PeriodStart]"); t.HasCheckConstraint("CK_ProductionOutputs_Quantities", "[TotalProducedQuantity] >= 0 AND [DefectQuantity] >= 0 AND [DefectQuantity] <= [TotalProducedQuantity]"); t.HasCheckConstraint("CK_ProductionOutputs_Durations", "[UnscheduledMinutes] >= 0 AND [StandardSpeedPerMinute] > 0"); });
        b.ConfigureAudited();
        b.Property(x => x.StandardSpeedPerMinute).HasPrecision(18, 4);
        b.Property(x => x.TotalProducedQuantity).HasPrecision(18, 4);
        b.Property(x => x.DefectQuantity).HasPrecision(18, 4);
        b.Property(x => x.UnscheduledMinutes).HasPrecision(18, 4);
        b.Property(x => x.SourceNote).HasMaxLength(500);
        b.HasIndex(x => x.ProductionPlanId);
        b.HasIndex(x => new { x.WorkUnitId, x.PeriodStart, x.PeriodEnd });
        b.HasOne(x => x.ProductionPlan).WithMany().HasForeignKey(x => x.ProductionPlanId).OnDelete(DeleteBehavior.NoAction); b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction); b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction); b.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.NoAction);
    }
}
