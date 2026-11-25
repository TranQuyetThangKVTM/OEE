using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Platform;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Lưu dấu vết quản trị riêng cho mỗi lần dọn AuditLog để lịch sử retention không tự xóa chính nó.</summary>
public sealed class AuditRetentionRunConfiguration : IEntityTypeConfiguration<AuditRetentionRunEntity>
{
    public void Configure(EntityTypeBuilder<AuditRetentionRunEntity> b)
    {
        b.ToTable("AuditRetentionRuns", "audit");
        b.ConfigureEntity();
        b.Property(x => x.ActorUserId).HasMaxLength(64);
        b.Property(x => x.ActorUserName).HasMaxLength(200).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => x.StartedAtUtc);
        b.HasIndex(x => x.CutoffUtc);
    }
}
