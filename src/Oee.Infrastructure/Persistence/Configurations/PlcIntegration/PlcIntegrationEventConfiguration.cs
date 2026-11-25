using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ lịch sử integration event persisted dùng cho RCA, tách khỏi ring buffer UI.</summary>
public sealed class PlcIntegrationEventConfiguration : IEntityTypeConfiguration<PlcIntegrationEventEntity>
{
    public void Configure(EntityTypeBuilder<PlcIntegrationEventEntity> b)
    {
        b.ToTable("PlcIntegrationEvents", "plc");
        b.ConfigureEntity();
        b.Property(x => x.Kind).HasMaxLength(64).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(4000);
        b.HasIndex(x => new { x.OccurredAt, x.Level });
        b.HasIndex(x => new { x.PlcDeviceMappingId, x.OccurredAt });
        b.HasOne(x => x.PlcDeviceMapping).WithMany().HasForeignKey(x => x.PlcDeviceMappingId).OnDelete(DeleteBehavior.NoAction);
    }
}
