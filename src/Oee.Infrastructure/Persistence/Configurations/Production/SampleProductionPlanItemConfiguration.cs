using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Production;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ dòng mẫu thiết bị × sản phẩm × ca và chống trùng đồng thời ở SQL.</summary>
public sealed class SampleProductionPlanItemConfiguration : IEntityTypeConfiguration<SampleProductionPlanItemEntity>
{
    public void Configure(EntityTypeBuilder<SampleProductionPlanItemEntity> b)
    {
        b.ToTable("SampleProductionPlanItems", "planning", t => t.HasCheckConstraint("CK_SampleProductionPlanItems_Quantity", "[PlannedQuantity] >= 0")); b.ConfigureSoftDeleted();
        b.Property(x => x.PlannedQuantity).HasPrecision(18, 4); b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => new { x.SampleProductionPlanId, x.WorkUnitId, x.ProductId, x.ShiftId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.SampleProductionPlan).WithMany(x => x.Items).HasForeignKey(x => x.SampleProductionPlanId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.NoAction);
    }
}
