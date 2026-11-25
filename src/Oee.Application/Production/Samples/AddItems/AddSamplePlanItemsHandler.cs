using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Common;
using Oee.Domain.Entities.Production;

namespace Oee.Application.Production.Samples.AddItems;

/// <summary>Điều phối thao tác AddSamplePlanItems trong cùng scope lưu dữ liệu.</summary>
internal sealed class AddSamplePlanItemsHandler(SampleProductionPlanContext context)
    : IRequestHandler<AddSamplePlanItemsCommand, SamplePlanBulkResult>
{
    public async Task<SamplePlanBulkResult> HandleAsync(
        AddSamplePlanItemsCommand command,
        CancellationToken cancellationToken)
    {
        var samplePlanId = command.SamplePlanId;
        var request = command.Request;
        SampleProductionPlanValidator.ValidateBulk(
            "existing",
            null,
            request.WorkUnitIds,
            request.ShiftIds,
            request.ProductId,
            request.PlannedQuantity,
            request.Notes,
            validateName: false);
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            _ = await context.Store.FindAsync(samplePlanId, true, cancellationToken)
                ?? throw new KeyNotFoundException("Kế hoạch mẫu không tồn tại hoặc đã ngừng sử dụng.");
            var combinations = await context.ResolveActiveCombinationsAsync(request.WorkUnitIds, request.ShiftIds, request.ProductId, cancellationToken);
            var unitIds = combinations.Select(x => x.WorkUnitId).Distinct().ToArray();
            var shiftIds = combinations.Select(x => x.ShiftId).Distinct().ToArray();
            // Một query lấy key đã có thay vì AnyAsync cho từng tổ hợp (tránh N+1).
            var existing = await context.Store.ReadExistingKeysAsync(samplePlanId, request.ProductId, unitIds, shiftIds, cancellationToken);
            var existingKeys = existing.Select(x => (x.WorkUnitId, x.ShiftId)).ToHashSet();
            var newCombinations = combinations.Where(x => !existingKeys.Contains((x.WorkUnitId, x.ShiftId))).ToArray();
            var skipped = combinations.Where(x => existingKeys.Contains((x.WorkUnitId, x.ShiftId)))
                .Select(x => $"{x.WorkUnitCode} × {x.ShiftCode} × SKU đã tồn tại trong mẫu.")
                .ToArray();
            var items = newCombinations.Select(x => SampleProductionPlanItemEntity.Create(
                    sampleProductionPlanId: samplePlanId,
                    workUnitId: x.WorkUnitId,
                    productId: request.ProductId,
                    shiftId: x.ShiftId,
                    plannedQuantity: request.PlannedQuantity,
                    notes: request.Notes))
                .ToArray();
            context.Store.AddItems(items);
            await context.TouchParentAsync(samplePlanId, cancellationToken);
            context.UnitOfWork.AddAudit(context.CreateBulkAudit(
                    "SampleProductionPlanItemsAdded",
                    samplePlanId,
                    combinations.Count,
                    items.Length,
                    skipped.Length));
            await context.UnitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SamplePlanBulkResult(
                samplePlanId,
                combinations.Count,
                items.Length,
                skipped.Length,
                items.Select(SampleProductionPlanContext.Map).ToArray(),
                skipped);
        }
        catch (PersistenceWriteException exception)
        {
            throw new InvalidOperationException(
                "Không thể thêm dòng vì một tổ hợp vừa được người dùng khác tạo. Không có dòng trùng nào được ghi.",
                exception);
        }
    }
}
