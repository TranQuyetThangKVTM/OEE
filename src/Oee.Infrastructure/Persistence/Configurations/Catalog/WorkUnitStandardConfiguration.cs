using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ phiên bản tốc độ chuẩn của cặp WorkUnit × Product.</summary>
/// <remarks>
/// Check constraint bảo vệ tốc độ/khoảng ngày; trigger SQL chặn các khoảng hiệu lực chồng nhau.
/// NoAction giữ nguyên fact/history khi master bị tạm ngưng hoặc soft-delete.
/// </remarks>
public sealed class WorkUnitStandardConfiguration : IEntityTypeConfiguration<WorkUnitStandardEntity>
{
    public void Configure(EntityTypeBuilder<WorkUnitStandardEntity> b)
    {
        b.ToTable("WorkUnitStandards", "catalog", t => { t.HasCheckConstraint("CK_WorkUnitStandards_Speed", "[StandardSpeedPerMinute] > 0"); t.HasCheckConstraint("CK_WorkUnitStandards_EffectiveRange", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"); t.HasTrigger("TR_WorkUnitStandards_PreventEffectiveOverlap"); t.UseSqlOutputClause(false); });
        b.ConfigureSoftDeleted();
        b.Property(x => x.StandardSpeedPerMinute).HasPrecision(18, 4);
        b.Property(x => x.Uom).HasMaxLength(16).IsRequired();
        b.Property(x => x.ApprovedBy).HasMaxLength(120);
        b.HasIndex(x => new { x.WorkUnitId, x.ProductId, x.EffectiveFrom }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
    }
}
