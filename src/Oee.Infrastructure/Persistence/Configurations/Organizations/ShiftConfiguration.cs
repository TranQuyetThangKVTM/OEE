using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ Shift của Site bằng TimeOnly, cho phép ca qua nửa đêm.</summary>
/// <remarks>
/// EndTime nhỏ hơn StartTime là hợp lệ (ví dụ 22:00–06:00); constraint chỉ cấm hai giờ bằng nhau.
/// Việc chuyển thành DateTimeOffset theo timezone Site do ShiftPeriodResolver thực hiện.
/// </remarks>
public sealed class ShiftConfiguration : IEntityTypeConfiguration<ShiftEntity>
{
    public void Configure(EntityTypeBuilder<ShiftEntity> b)
    {
        b.ToTable("Shifts", "org", t => t.HasCheckConstraint("CK_Shifts_DifferentTimes", "[StartTime] <> [EndTime]"));
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.SiteId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.NoAction);
    }
}
