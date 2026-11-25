using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ Site thuộc Enterprise; code chỉ unique trong cùng Enterprise.</summary>
public sealed class SiteConfiguration : IEntityTypeConfiguration<SiteEntity>
{
    public void Configure(EntityTypeBuilder<SiteEntity> b)
    {
        b.ToTable("Sites", "org");
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.EnterpriseId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Enterprise).WithMany().HasForeignKey(x => x.EnterpriseId).OnDelete(DeleteBehavior.NoAction);
    }
}
