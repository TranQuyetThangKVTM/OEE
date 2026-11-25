using System.Text.Json;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Platform;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Operations;

/// <summary>Giữ chung persistence port, unit of work, actor, quyền và audit cho một thao tác master.</summary>
internal sealed class MasterDataCommandContext(
    IMasterDataStore store,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor? currentActor = null,
    ICapabilityChecker? capabilityChecker = null)
    : IUseCaseContext
{
    internal IMasterDataStore Store => store;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;

    internal static async Task UniqueAsync(Func<Task<bool>> exists, string message)
    {
        if (await exists())
            throw new InvalidOperationException(message);
    }

    internal static async Task EnsureActiveAsync(
        Func<Task<bool>> exists,
        Guid id,
        string name)
    {
        if (id == Guid.Empty || !await exists())
            throw new KeyNotFoundException($"{name} was not found or is inactive.");
    }

    internal static async Task EnsureParentActiveAsync(
        Func<Task<bool>> exists,
        Guid id,
        string name)
    {
        if (id == Guid.Empty || !await exists())
            throw new InvalidOperationException($"{name} is inactive. Activate the parent first.");
    }

    /// <summary>Dùng chung luồng Activate/Deactivate với RowVersion, dependency rule và semantic audit.</summary>
    internal async Task<MasterMutationResult> SetStatusAsync<TEntity>(
        Func<CancellationToken, Task<TEntity?>> find,
        string targetStatus,
        string rowVersion,
        string entityName,
        Func<TEntity, RecordStatusEnum, Task> validate,
        CancellationToken cancellationToken)
    where TEntity : SoftDeletableAuditedEntity, IStatusEntity
    {
        EnsureAuthorized();
        var target = Status(targetStatus);
        var entity = await find(cancellationToken) ?? throw Missing(entityName);
        var before = entity.Status;

        if (before == target)
            return Result(entity);
        ApplyConcurrency(entity, rowVersion);
        await validate(entity, target);
        entity.ChangeStatus(target);
        Touch(entity);
        AddSemanticAudit(
            target == RecordStatusEnum.Active ? "Activate" : "Deactivate",
            entity,
            new
            {
                Status = before.ToString()
            },
            new
            {
                Status = target.ToString()
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }

    /// <summary>Thêm audit nghiệp vụ rõ action before/after vào cùng đơn vị ghi dữ liệu.</summary>
    internal void AddSemanticAudit(
        string action,
        SoftDeletableAuditedEntity entity,
        object before,
        object after)
    {
        unitOfWork.AddAudit(AuditLogEntity.Create(
                timestamp: clock.UtcNow,
                action: action,
                entityType: EntityTypeName.Get(entity.GetType()),
                entityId: entity.Id.ToString(),
                userId: currentActor?.UserId,
                userName: currentActor?.UserName ?? "System",
                entitySubject: entity.GetType().GetProperty("Code")?.GetValue(entity)?.ToString(),
                changes: JsonSerializer.Serialize(new
                    {
                        Before = before,
                        After = after
                    }),
                ipAddress: currentActor?.IpAddress));
    }

    /// <summary>Kiểm tra MasterDataWrite ở server; ẩn nút trên UI chỉ là lớp trải nghiệm.</summary>
    internal void EnsureAuthorized()
    {
        if (capabilityChecker is not null && !capabilityChecker.HasCapability(Capabilities.MasterDataWrite))
            throw new UnauthorizedAccessException("The current user is not authorized to manage master data.");
    }

    /// <summary>Decode token và yêu cầu persistence kiểm tra optimistic concurrency.</summary>
    internal void ApplyConcurrency(SoftDeletableAuditedEntity entity, string rowVersion)
    {
        byte[] expected;

        try
        {
            expected = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ArgumentException("RowVersion is invalid.", nameof(rowVersion));
        }

        unitOfWork.ExpectRowVersion(entity, expected);
    }

    internal void SoftDelete(SoftDeletableAuditedEntity entity, string rowVersion)
    {
        ApplyConcurrency(entity, rowVersion);
        entity.SoftDelete(clock.UtcNow);
    }

    internal void Touch(AuditedEntity entity) => entity.RecordUpdate(clock.UtcNow);

    /// <summary>Chuyển unique/FK race lúc create thành thông báo use case thân thiện.</summary>
    internal async Task SaveCreateAsync(string subject, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceWriteException exception)
        {
            throw new InvalidOperationException(
                $"{subject} could not be created because its code or relationship conflicts with data saved by another request.",
                exception);
        }
    }
}
