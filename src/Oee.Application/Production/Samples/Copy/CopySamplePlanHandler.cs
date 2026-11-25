using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Common;
using Oee.Domain.Entities.Production;

namespace Oee.Application.Production.Samples.Copy;

/// <summary>Điều phối thao tác CopySamplePlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class CopySamplePlanHandler(SampleProductionPlanContext context)
    : IRequestHandler<CopySamplePlanCommand, SamplePlanResponse>
{
    public async Task<SamplePlanResponse> HandleAsync(
        CopySamplePlanCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var code = command.Code;
        var name = command.Name;
        SampleProductionPlanValidator.Validate(new SamplePlanRequest(code, name, null));
        var source = await context.Store.FindAsync(id, false, cancellationToken)
            ?? throw new KeyNotFoundException("Sample production plan was not found.");
        var sourceItems = await context.Store.ReadItemsAsync(id, false, cancellationToken);

        var copy = SampleProductionPlanEntity.Create(code: code, name: name, description: source.Description);

        context.Store.Add(copy);

        // Chỉ Add vào ChangeTracker trong vòng lặp; SaveChanges được gọi một lần sau khi dựng đủ graph.
        foreach (var sourceItem in sourceItems)
        {
            context.Store.AddItem(SampleProductionPlanItemEntity.Create(
                    sampleProductionPlanId: copy.Id,
                    workUnitId: sourceItem.WorkUnitId,
                    productId: sourceItem.ProductId,
                    shiftId: sourceItem.ShiftId,
                    plannedQuantity: sourceItem.PlannedQuantity,
                    notes: sourceItem.Notes));
        }

        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return SampleProductionPlanContext.Map(copy);
    }
}
