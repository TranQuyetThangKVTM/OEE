using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Production;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ raw delta PLC chưa đủ bằng chứng gắn vào active ProductionPlan.</summary>
/// <remarks>SourceKey unique chống ghi hai lần khi Worker replay/restart.</remarks>
public sealed class ProductionReconciliationItemConfiguration : IEntityTypeConfiguration<ProductionReconciliationItemEntity>
{
    public void Configure(EntityTypeBuilder<ProductionReconciliationItemEntity> b)
    {
        b.ToTable("ProductionReconciliationItems", "production", t => t.HasCheckConstraint("CK_ProductionReconciliationItems_Deltas", "[TotalDelta] >= 0 AND [DefectDelta] >= 0 AND [DefectDelta] <= [TotalDelta]"));
        b.ConfigureAudited();
        b.Property(x => x.SourceKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.TotalDelta).HasPrecision(18, 4);
        b.Property(x => x.DefectDelta).HasPrecision(18, 4);
        b.Property(x => x.ReasonCode).HasMaxLength(64).IsRequired();
        b.Property(x => x.ResolvedBy).HasMaxLength(120);
        b.HasIndex(x => x.SourceKey).IsUnique();
        b.HasIndex(x => new { x.State, x.CapturedAt });
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ResolvedProductionPlan).WithMany().HasForeignKey(x => x.ResolvedProductionPlanId).OnDelete(DeleteBehavior.NoAction);
    }
}
