using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ tag động theo TagRole + Direction thay vì các cột NodeId cố định.</summary>
public sealed class OpcUaTagMappingConfiguration : IEntityTypeConfiguration<OpcUaTagMappingEntity>
{
    public void Configure(EntityTypeBuilder<OpcUaTagMappingEntity> b)
    {
        b.ToTable("OpcUaTagMappings", "plc");
        b.ConfigureSoftDeleted();
        b.Property(x => x.NodeId).HasMaxLength(512).IsRequired();
        b.HasIndex(x => new { x.PlcDeviceMappingId, x.TagRole, x.Direction }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.PlcDeviceMapping).WithMany().HasForeignKey(x => x.PlcDeviceMappingId).OnDelete(DeleteBehavior.NoAction);
    }
}
