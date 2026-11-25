using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ lịch sử vòng đời session connect/disconnect/reconnect theo OPC connection.</summary>
public sealed class PlcSessionEventConfiguration : IEntityTypeConfiguration<PlcSessionEventEntity>
{
    public void Configure(EntityTypeBuilder<PlcSessionEventEntity> b)
    {
        b.ToTable("PlcSessionEvents", "plc");
        b.ConfigureEntity();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(4000);
        b.HasIndex(x => new { x.OpcUaConnectionId, x.OccurredAt });
        b.HasOne(x => x.OpcUaConnection).WithMany().HasForeignKey(x => x.OpcUaConnectionId).OnDelete(DeleteBehavior.NoAction);
    }
}
