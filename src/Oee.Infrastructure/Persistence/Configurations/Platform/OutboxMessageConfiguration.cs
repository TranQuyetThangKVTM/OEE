using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Platform;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ outbox store-and-forward với idempotency key duy nhất.</summary>
/// <remarks>Index ProcessedAt/OccurredAt/ExpiresAt phục vụ replay và retention mà không quét toàn bảng.</remarks>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> b)
    {
        b.ToTable("OutboxMessages", "integration", t => t.HasCheckConstraint("CK_OutboxMessages_PayloadJson", "ISJSON([Payload]) = 1"));
        b.ConfigureEntity();
        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.Type).HasMaxLength(500).IsRequired();
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(4000);
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
        b.HasIndex(x => new { x.ProcessedAt, x.ExpiresAt });
    }
}
