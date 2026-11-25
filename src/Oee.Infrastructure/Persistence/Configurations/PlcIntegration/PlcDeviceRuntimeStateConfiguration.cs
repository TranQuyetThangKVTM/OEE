using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ runtime state tần suất cao tách khỏi device configuration ít thay đổi.</summary>
public sealed class PlcDeviceRuntimeStateConfiguration : IEntityTypeConfiguration<PlcDeviceRuntimeStateEntity>
{
    public void Configure(EntityTypeBuilder<PlcDeviceRuntimeStateEntity> b)
    {
        b.ToTable("PlcDeviceRuntimeStates", "plc");
        b.ConfigureEntity();
        b.Property(x => x.LastFaultCode).HasMaxLength(64);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.PlcDeviceMappingId).IsUnique();
        b.HasOne(x => x.PlcDeviceMapping).WithOne().HasForeignKey<PlcDeviceRuntimeStateEntity>(x => x.PlcDeviceMappingId).OnDelete(DeleteBehavior.NoAction);
    }
}
