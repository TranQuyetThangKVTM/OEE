using System.Data;
using System.Text.Json;
using Oee.Application.Audit;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Security;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;

namespace Oee.Application.Production.Samples.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác SampleProductionPlan.</summary>
internal sealed class SampleProductionPlanContext(
    ISampleProductionPlanStore store,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor? currentActor = null)
    : IUseCaseContext
{
    internal ISampleProductionPlanStore Store => store;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal IClock Clock => clock;

    internal async Task<SampleProductionPlanEntity> FindAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await store.FindAsync(id, true, cancellationToken)
            ?? throw new KeyNotFoundException("Sample production plan was not found.");

    internal async Task TouchParentAsync(Guid id, CancellationToken cancellationToken)
    {
        var parent = await FindAsync(id, cancellationToken);
        parent.RecordUpdate(clock.UtcNow);
    }

    internal async Task ValidateReferencesAsync(
        SamplePlanItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!await store.IsActiveUnitAsync(request.WorkUnitId, cancellationToken))
            throw new KeyNotFoundException("Work unit was not found or is inactive.");

        if (!await store.IsActiveProductAsync(request.ProductId, cancellationToken))
            throw new KeyNotFoundException("Product was not found or is inactive.");

        if (!await store.IsActiveShiftAsync(request.ShiftId, cancellationToken))
            throw new KeyNotFoundException("Shift was not found or is inactive.");
    }

    internal static SamplePlanResponse Map(SampleProductionPlanEntity item)
        => new(item.Id, item.Code, item.Name, item.Description, Convert.ToBase64String(item.RowVersion));

    internal static SamplePlanItemResponse Map(SampleProductionPlanItemEntity item)
        => new(
            item.Id,
            item.SampleProductionPlanId,
            item.WorkUnitId,
            item.ProductId,
            item.ShiftId,
            item.PlannedQuantity,
            item.Notes,
            Convert.ToBase64String(item.RowVersion));

    internal async Task<IReadOnlyCollection<SamplePlanCombination>> ResolveActiveCombinationsAsync(
        IReadOnlyCollection<Guid> requestedWorkUnits,
        IReadOnlyCollection<Guid> requestedShifts,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var workUnitIds = requestedWorkUnits.Where(x => x != Guid.Empty).Distinct().ToArray();
        var shiftIds = requestedShifts.Where(x => x != Guid.Empty).Distinct().ToArray();
        // Projection chỉ lấy Id/Code/SiteId cần thiết; EF không materialize navigation graph.
        var workUnits = await store.ReadActiveUnitsAsync(workUnitIds, cancellationToken);
        var shifts = await store.ReadActiveShiftsAsync(shiftIds, cancellationToken);

        if (workUnits.Length != workUnitIds.Length)
            throw new KeyNotFoundException("Một hoặc nhiều thiết bị không tồn tại hoặc không còn Active.");

        if (shifts.Length != shiftIds.Length)
            throw new KeyNotFoundException("Một hoặc nhiều ca không tồn tại hoặc không còn Active.");

        if (!await store.IsActiveProductAsync(productId, cancellationToken))
            throw new KeyNotFoundException("SKU không tồn tại hoặc không còn Active.");
        // Tích Descartes được dựng sau khi hai lookup nhỏ đã tải; mỗi cặp trở thành một dòng mẫu.
        var combinations = (
        from workUnit in workUnits
        from shift in shifts
        select new SamplePlanCombination(workUnit.Id, workUnit.Code, workUnit.SiteId, shift.Id, shift.Code, shift.SiteId)).ToArray();

        if (combinations.Any(x => x.WorkUnitSiteId != x.ShiftSiteId))
            throw new InvalidOperationException("Thiết bị và ca phải thuộc cùng một nhà máy; toàn bộ giao dịch đã được hoàn tác.");

        return combinations;
    }

    internal AuditLogEntity CreateBulkAudit(
        string action,
        Guid samplePlanId,
        int requested,
        int created,
        int skipped)
        => AuditLogEntity.Create(
            timestamp: clock.UtcNow,
            action: action,
            entityType: "SampleProductionPlan",
            entityId: samplePlanId.ToString(),
            userId: currentActor?.UserId,
            userName: currentActor?.UserName ?? "system",
            entitySubject: samplePlanId.ToString(),
            changes: JsonSerializer.Serialize(new
                {
                    SampleProductionPlanId = samplePlanId,
                    RequestedCount = requested,
                    CreatedCount = created,
                    SkippedCount = skipped,

                }),
            ipAddress: AuditIpAddress.Normalize(currentActor?.IpAddress));

}
