using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ Area thuộc Site; delete NoAction ngăn xóa dây chuyền lịch sử theo parent.</summary>
public sealed class AreaConfiguration : IEntityTypeConfiguration<AreaEntity>
{
    public void Configure(EntityTypeBuilder<AreaEntity> b)
    {
        b.ToTable("Areas", "org");
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.SiteId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.NoAction);
    }
}
