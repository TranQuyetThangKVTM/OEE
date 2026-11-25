using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Common;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>
/// Các quy ước EF Core dùng chung cho ba mức base entity của Domain.
/// </summary>
/// <remarks>
/// Extension nằm ở Infrastructure vì Domain không được biết HasKey, RowVersion hay query filter.
/// Các configuration cụ thể gọi lại quy ước này để tránh lặp cấu hình kỹ thuật.
/// </remarks>
internal static class ConfigurationExtensions
{
    /// <summary>Lưu trạng thái enum của danh mục vào cột int hiện hữu.</summary>
    public static void ConfigureStatus<T>(this EntityTypeBuilder<T> builder)
        where T : SoftDeletableAuditedEntity, IStatusEntity
    {
        builder.Property(entity => entity.Status).HasConversion<int>();
    }

    /// <summary>Cấu hình Guid Id là khóa chính do ứng dụng cấp, không do SQL sinh.</summary>
    public static void ConfigureEntity<T>(this EntityTypeBuilder<T> builder)
        where T : Entity
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
    }

    /// <summary>Cấu hình khóa và giới hạn độ dài actor cho entity có metadata audit.</summary>
    public static void ConfigureAudited<T>(this EntityTypeBuilder<T> builder)
        where T : AuditedEntity
    {
        builder.ConfigureEntity();
        builder.Property(entity => entity.CreatedBy).HasMaxLength(120);
        builder.Property(entity => entity.UpdatedBy).HasMaxLength(120);
    }

    /// <summary>
    /// Cấu hình optimistic concurrency bằng SQL rowversion và mặc định ẩn dữ liệu soft-deleted.
    /// </summary>
    public static void ConfigureSoftDeleted<T>(this EntityTypeBuilder<T> builder)
        where T : SoftDeletableAuditedEntity
    {
        builder.ConfigureAudited();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasQueryFilter(entity => !entity.IsDeleted);
    }
}
