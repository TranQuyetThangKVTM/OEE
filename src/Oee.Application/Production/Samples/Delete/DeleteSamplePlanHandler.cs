using Oee.Application.Common;
using Oee.Application.Production.Samples.Common;

namespace Oee.Application.Production.Samples.Delete;

/// <summary>Điều phối thao tác DeleteSamplePlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteSamplePlanHandler(SampleProductionPlanContext context)
    : ICommandHandler<DeleteSamplePlanCommand>
{
    public async Task HandleAsync(DeleteSamplePlanCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var expectedRowVersion = command.ExpectedRowVersion;
        var item = await context.FindAsync(id, cancellationToken);
        context.UnitOfWork.ExpectRowVersion(item, expectedRowVersion);
        item.SoftDelete(context.Clock.UtcNow);
        var children = await context.Store.ReadItemsAsync(id, true, cancellationToken);

        foreach (var child in children)
        {
            child.SoftDelete(context.Clock.UtcNow);
        }

        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
