using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ một WorkUnit với một external device trên OPC UA connection.</summary>
/// <remarks>Unique WorkUnitId ngăn hai mapping active cùng điều khiển một máy.</remarks>
public sealed class PlcDeviceMappingConfiguration : IEntityTypeConfiguration<PlcDeviceMappingEntity>
{
    public void Configure(EntityTypeBuilder<PlcDeviceMappingEntity> b)
    {
        b.ToTable("PlcDeviceMappings", "plc", t => t.HasCheckConstraint("CK_PlcDeviceMappings_DebounceSamples", "[DebounceSamples] > 0"));
        b.ConfigureSoftDeleted();
        b.Property(x => x.ExternalDeviceId).HasMaxLength(128).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.OpcUaConnectionId, x.ExternalDeviceId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.WorkUnitId).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.OpcUaConnection).WithMany().HasForeignKey(x => x.OpcUaConnectionId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.DefaultProduct).WithMany().HasForeignKey(x => x.DefaultProductId).OnDelete(DeleteBehavior.NoAction);
    }
}
