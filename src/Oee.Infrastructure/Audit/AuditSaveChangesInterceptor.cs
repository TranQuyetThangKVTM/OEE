using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oee.Application.Audit;
using Oee.Application.Common;
using Oee.Application.Security;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Identity;

namespace Oee.Infrastructure.Audit;

/// <summary>
/// EF Core SaveChanges interceptor tự động thêm AuditLog cho mutation nghiệp vụ.
/// </summary>
/// <remarks>
/// Interceptor chạy ngay trước SQL SaveChanges. AuditLog được thêm vào cùng DbContext nên cùng
/// transaction: mutation rollback thì audit “thành công” cũng rollback. Runtime PLC tần suất cao,
/// Identity secret, outbox và chính audit được loại trừ để tránh noise/duplicate/rò dữ liệu.
/// </remarks>
public sealed class AuditSaveChangesInterceptor(ICurrentActor currentActor) : SaveChangesInterceptor
{
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "Token", "Value", "Secret",
        "ClientSecret", "CertificatePassword", "PrivateKey", "ConnectionString",
    };

    /// <summary>Chèn audit trước SaveChanges đồng bộ.</summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>Chèn audit trước SaveChangesAsync; token được truyền tiếp cho EF.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>Thu thập snapshot ChangeTracker và thêm audit entries đúng một lần.</summary>
    private void AppendAuditEntries(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        if (dbContext.ChangeTracker.Entries<AuditLogEntity>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Audit logs are append-only. Use the authorized retention service for age-based cleanup.");
        }

        // Materialize trước AddRange để audit mới không tự bị vòng lặp hiện tại audit lại.
        var auditEntries = dbContext.ChangeTracker.Entries()
            .Where(ShouldAudit)
            .Select(CreateAuditLog)
            .ToArray();

        if (auditEntries.Length > 0)
        {
            dbContext.Set<AuditLogEntity>().AddRange(auditEntries);
        }
    }

    /// <summary>Chỉ chọn Added/Modified/Deleted nghiệp vụ và loại trừ các nguồn nhạy cảm/noisy.</summary>
    private static bool ShouldAudit(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
        && entry.Entity is not AuditLogEntity
        && entry.Entity is not AuditRetentionRunEntity
        && entry.Entity is not ApplicationUser
        && !string.Equals(entry.Metadata.ClrType.Namespace, "Microsoft.AspNetCore.Identity", StringComparison.Ordinal)
        && entry.Entity is not OutboxMessageEntity
        && entry.Entity is not PlcDeviceRuntimeStateEntity
        && entry.Entity is not CounterCheckpointEntity
        && entry.Entity is not PlcIntegrationEventEntity
        && entry.Entity is not PlcSessionEventEntity;

    /// <summary>Tạo snapshot before/after chỉ cho field thay đổi và đã loại dữ liệu nhạy cảm.</summary>
    private AuditLogEntity CreateAuditLog(EntityEntry entry)
    {
        var isSoftDelete = IsSoftDelete(entry);
        var primaryKey = entry.Metadata.FindPrimaryKey();
        var entityId = primaryKey is null
            ? string.Empty
            : string.Join("|", primaryKey.Properties.Select(property => entry.Property(property.Name).CurrentValue));

        var changes = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        // Modified bình thường chỉ ghi property.IsModified; soft-delete ghi đầy đủ snapshot trước xóa.
        foreach (var property in entry.Properties.Where(property => !IsSensitive(property.Metadata.Name)))
        {
            if (entry.State == EntityState.Modified && !isSoftDelete && !property.IsModified)
            {
                continue;
            }

            changes[property.Metadata.Name] = new
            {
                Before = entry.State == EntityState.Added ? null : NormalizeValue(property.OriginalValue),
                After = entry.State == EntityState.Deleted || isSoftDelete
                    ? null
                    : NormalizeValue(property.CurrentValue),
            };
        }

        return AuditLogEntity.Create(
            timestamp: DateTimeOffset.UtcNow,
            action: isSoftDelete ? EntityState.Deleted.ToString() : entry.State.ToString(),
            entityType: EntityTypeName.Get(entry.Metadata.ClrType),
            entityId: entityId,
            userId: currentActor.UserId,
            userName: currentActor.UserName,
            entitySubject: ResolveSubject(entry),
            changes: JsonSerializer.Serialize(changes),
            ipAddress: AuditIpAddress.Normalize(currentActor.IpAddress));
    }

    /// <summary>Nhận diện transition IsDeleted false → true để action hiển thị là Deleted.</summary>
    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified)
        {
            return false;
        }

        var property = entry.Properties.FirstOrDefault(candidate =>
            candidate.Metadata.Name.Equals("IsDeleted", StringComparison.Ordinal));
        return property is { IsModified: true }
            && property.OriginalValue is false
            && property.CurrentValue is true;
    }

    private static bool IsSensitive(string propertyName) =>
        SensitiveProperties.Contains(propertyName)
        || propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase);

    // Binary token không cần hiển thị; DateTimeOffset được thống nhất UTC trong JSON audit.
    private static object? NormalizeValue(object? value) => value switch
    {
        byte[] => "[binary]",
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
        _ => value,
    };

    private static string? ResolveSubject(EntityEntry entry)
    {
        var subjectProperty = entry.Properties.FirstOrDefault(property =>
            property.Metadata.Name.Equals("Code", StringComparison.Ordinal)
            || property.Metadata.Name.Equals("UserName", StringComparison.Ordinal)
            || property.Metadata.Name.Equals("Name", StringComparison.Ordinal));
        return subjectProperty?.CurrentValue?.ToString() is { } value
            ? value[..Math.Min(value.Length, 300)]
            : null;
    }

}
