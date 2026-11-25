using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Production;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ phần cha của kế hoạch mẫu.</summary>
public sealed class SampleProductionPlanConfiguration : IEntityTypeConfiguration<SampleProductionPlanEntity>
{
    public void Configure(EntityTypeBuilder<SampleProductionPlanEntity> b)
    {
        b.ToTable("SampleProductionPlans", "planning");
        b.ConfigureSoftDeleted();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
