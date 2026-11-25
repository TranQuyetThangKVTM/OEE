using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ checkpoint counter để Worker restart/replay không cộng trùng.</summary>
public sealed class CounterCheckpointConfiguration : IEntityTypeConfiguration<CounterCheckpointEntity>
{
    public void Configure(EntityTypeBuilder<CounterCheckpointEntity> b)
    {
        b.ToTable("CounterCheckpoints", "plc");
        b.ConfigureEntity();
        b.Property(x => x.LastTotalCount).HasPrecision(18, 4);
        b.Property(x => x.LastDefectCount).HasPrecision(18, 4);
        b.Property(x => x.LastSampleKey).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.PlcDeviceMappingId).IsUnique();
        b.HasOne(x => x.PlcDeviceMapping).WithOne().HasForeignKey<CounterCheckpointEntity>(x => x.PlcDeviceMappingId).OnDelete(DeleteBehavior.NoAction);
    }
}
