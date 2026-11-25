using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Common;

namespace Oee.Application.Production.Samples.PreviewAddItems;

/// <summary>Điều phối thao tác PreviewSamplePlanItems trong cùng scope lưu dữ liệu.</summary>
internal sealed class PreviewSamplePlanItemsHandler(SampleProductionPlanContext context)
    : IRequestHandler<PreviewSamplePlanItemsQuery, SamplePlanBulkPreview>
{
    public async Task<SamplePlanBulkPreview> HandleAsync(
        PreviewSamplePlanItemsQuery command,
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
        _ = await context.Store.FindAsync(samplePlanId, false, cancellationToken)
            ?? throw new KeyNotFoundException("Kế hoạch mẫu không tồn tại hoặc đã ngừng sử dụng.");
        var combinations = await context.ResolveActiveCombinationsAsync(request.WorkUnitIds, request.ShiftIds, request.ProductId, cancellationToken);
        var unitIds = combinations.Select(x => x.WorkUnitId).Distinct().ToArray();
        var shiftIds = combinations.Select(x => x.ShiftId).Distinct().ToArray();
        var existing = await context.Store.ReadExistingKeysAsync(samplePlanId, request.ProductId, unitIds, shiftIds, cancellationToken);
        var keys = existing.Select(x => (x.WorkUnitId, x.ShiftId)).ToHashSet();
        var duplicateReasons = combinations.Where(x => keys.Contains((x.WorkUnitId, x.ShiftId)))
            .Select(x => $"{x.WorkUnitCode} × {x.ShiftCode} × SKU đã tồn tại trong mẫu.")
            .ToArray();

        return new SamplePlanBulkPreview(
            combinations.Count,
            combinations.Count - duplicateReasons.Length,
            duplicateReasons.Length,
            duplicateReasons);
    }
}
