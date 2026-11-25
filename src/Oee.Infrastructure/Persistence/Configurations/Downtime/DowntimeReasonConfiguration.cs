using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ danh mục lý do dừng và unique code cho bản ghi chưa xóa.</summary>
public sealed class DowntimeReasonConfiguration : IEntityTypeConfiguration<DowntimeReasonEntity>
{
    public void Configure(EntityTypeBuilder<DowntimeReasonEntity> b)
    {
        b.ToTable("DowntimeReasons", "catalog");
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
