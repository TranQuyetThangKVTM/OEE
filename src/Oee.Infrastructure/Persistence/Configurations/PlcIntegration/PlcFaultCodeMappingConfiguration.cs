using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ fault code active ở scope WorkUnit cụ thể hoặc global.</summary>
/// <remarks>Hai filtered unique index xử lý NULL scope đúng theo SQL Server.</remarks>
public sealed class PlcFaultCodeMappingConfiguration : IEntityTypeConfiguration<PlcFaultCodeMappingEntity>
{
    public void Configure(EntityTypeBuilder<PlcFaultCodeMappingEntity> b)
    {
        b.ToTable("PlcFaultCodeMappings", "plc");
        b.ConfigureSoftDeleted();
        b.Property(x => x.FaultCode).HasMaxLength(64).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.FaultCode).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1 AND [WorkUnitId] IS NULL");
        b.HasIndex(x => new { x.WorkUnitId, x.FaultCode }).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1 AND [WorkUnitId] IS NOT NULL");
        b.HasOne(x => x.WorkUnit).WithMany().HasForeignKey(x => x.WorkUnitId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.DowntimeReason).WithMany().HasForeignKey(x => x.DowntimeReasonId).OnDelete(DeleteBehavior.NoAction);
    }
}
