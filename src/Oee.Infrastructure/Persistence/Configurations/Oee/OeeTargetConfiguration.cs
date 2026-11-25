using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Oee;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ target có version/effectivity ở scope WorkUnit, Site hoặc global.</summary>
public sealed class OeeTargetConfiguration : IEntityTypeConfiguration<OeeTargetEntity>
{
    private const string RatioCheck = "[MinimumOee] BETWEEN 0 AND 1 AND [MinimumAvailability] BETWEEN 0 AND 1 AND [MinimumPerformance] BETWEEN 0 AND 1 AND [MinimumQuality] BETWEEN 0 AND 1";
    public void Configure(EntityTypeBuilder<OeeTargetEntity> b)
    {
        b.ToTable("OeeTargets", "oee", t => { t.HasCheckConstraint("CK_OeeTargets_Ratios", RatioCheck); t.HasCheckConstraint("CK_OeeTargets_EffectiveRange", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"); t.HasCheckConstraint("CK_OeeTargets_Scope", "NOT ([WorkUnitId] IS NOT NULL AND [SiteId] IS NOT NULL)"); t.HasTrigger("TR_OeeTargets_PreventEffectiveOverlap"); t.UseSqlOutputClause(false); });
        b.ConfigureSoftDeleted(); b.Property(x => x.PeriodCode).HasMaxLength(40).IsRequired();
        b.Property(x => x.MinimumOee).HasPrecision(9, 6);
        b.Property(x => x.MinimumAvailability).HasPrecision(9, 6);
        b.Property(x => x.MinimumPerformance).HasPrecision(9, 6);
        b.Property(x => x.MinimumQuality).HasPrecision(9, 6);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.WorkUnitId, x.SiteId, x.PeriodType, x.PeriodCode, x.EffectiveFrom }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.NoAction);
    }
}
