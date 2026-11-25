using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Production;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ ProductionPlan có version, optimistic concurrency và truy vết mẫu nguồn.</summary>
/// <remarks>
/// Các FK đều NoAction để không cascade xóa lịch sử. Index WorkUnit + period hỗ trợ phát hiện
/// xung đột kế hoạch; VersionSeriesId + Version giữ chuỗi correction/version duy nhất.
/// </remarks>
public sealed class ProductionPlanConfiguration : IEntityTypeConfiguration<ProductionPlanEntity>
{
    public void Configure(EntityTypeBuilder<ProductionPlanEntity> b)
    {
        b.ToTable("ProductionPlans", "planning", t => { t.HasCheckConstraint("CK_ProductionPlans_Period", "[PlannedEnd] > [PlannedStart]"); t.HasCheckConstraint("CK_ProductionPlans_Quantity", "[PlannedQuantity] >= 0"); t.HasCheckConstraint("CK_ProductionPlans_Version", "[Version] > 0"); });
        b.ConfigureSoftDeleted();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.PlannedQuantity).HasPrecision(18, 4);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.DeletedAt).HasColumnName("CancelledAt");
        // Cột cũ chỉ thuộc persistence để mở database hiện hữu mà không cần đổi schema.
        b.Property<int>("Status");
        b.Property<string>("CancellationReason").HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.VersionSeriesId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.WorkUnitId, x.PlannedStart, x.PlannedEnd });
        b.HasIndex(x => new { x.SourceSampleProductionPlanId, x.SourceSampleProductionPlanItemId });
        b.HasOne(x => x.ReplacesPlan).WithMany().HasForeignKey(x => x.ReplacesPlanId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.SourceSampleProductionPlan).WithMany().HasForeignKey(x => x.SourceSampleProductionPlanId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.SourceSampleProductionPlanItem).WithMany().HasForeignKey(x => x.SourceSampleProductionPlanItemId).OnDelete(DeleteBehavior.NoAction);
    }
}
