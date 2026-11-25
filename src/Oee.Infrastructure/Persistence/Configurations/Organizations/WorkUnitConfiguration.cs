using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ máy/dây chuyền WorkUnit thuộc một WorkCenter.</summary>
public sealed class WorkUnitConfiguration : IEntityTypeConfiguration<WorkUnitEntity>
{
    public void Configure(EntityTypeBuilder<WorkUnitEntity> b)
    {
        b.ToTable("WorkUnits", "org");
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.WorkCenterId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.WorkCenter).WithMany().HasForeignKey(x => x.WorkCenterId).OnDelete(DeleteBehavior.NoAction);
    }
}
