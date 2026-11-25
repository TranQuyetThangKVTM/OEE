using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ WorkCenter thuộc Area với business key AreaId + Code.</summary>
public sealed class WorkCenterConfiguration : IEntityTypeConfiguration<WorkCenterEntity>
{
    public void Configure(EntityTypeBuilder<WorkCenterEntity> b)
    {
        b.ToTable("WorkCenters", "org");
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.AreaId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
    }
}
