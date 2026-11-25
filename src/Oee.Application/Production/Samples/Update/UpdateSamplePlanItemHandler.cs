using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Common;

namespace Oee.Application.Production.Samples.Update;

/// <summary>Điều phối thao tác UpdateSamplePlanItem trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateSamplePlanItemHandler(SampleProductionPlanContext context)
    : IRequestHandler<UpdateSamplePlanItemCommand, SamplePlanItemResponse>
{
    public async Task<SamplePlanItemResponse> HandleAsync(
        UpdateSamplePlanItemCommand command,
        CancellationToken cancellationToken)
    {
        var itemId = command.ItemId;
        var request = command.Request;
        var expectedRowVersion = command.ExpectedRowVersion;
        SampleProductionPlanValidator.Validate(request);
        await context.ValidateReferencesAsync(request, cancellationToken);
        var item = await context.Store.FindItemAsync(itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Sample production plan item was not found.");
        context.UnitOfWork.ExpectRowVersion(item, expectedRowVersion);
        item.Update(
            request.WorkUnitId,
            request.ProductId,
            request.ShiftId,
            request.PlannedQuantity,
            request.Notes);
        item.RecordUpdate(context.Clock.UtcNow);
        await context.TouchParentAsync(item.SampleProductionPlanId, cancellationToken);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return SampleProductionPlanContext.Map(item);
    }
}
