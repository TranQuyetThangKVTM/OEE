using System.Data;
using Oee.Application.Common;
using Oee.Application.Production.Samples.Common;

namespace Oee.Application.Production.Samples.Delete;

/// <summary>Điều phối thao tác DeleteSamplePlanItems trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteSamplePlanItemsHandler(SampleProductionPlanContext context)
    : ICommandHandler<DeleteSamplePlanItemsCommand>
{
    public async Task HandleAsync(
        DeleteSamplePlanItemsCommand command,
        CancellationToken cancellationToken)
    {
        var requests = command.Requests;

        if (requests.Count == 0)
            return;
        var ids = requests.Select(x => x.Id).Distinct().ToArray();
        var items = await context.Store.ReadItemsByIdsAsync(ids, cancellationToken);

        if (items.Length != ids.Length)
            throw new KeyNotFoundException("One or more sample-plan items were not found.");

        foreach (var item in items)
        {
            var request = requests.Single(x => x.Id == item.Id);
            context.UnitOfWork.ExpectRowVersion(item, Convert.FromBase64String(request.RowVersion));
            item.SoftDelete(context.Clock.UtcNow);
        }

        foreach (var sampleId in items.Select(x => x.SampleProductionPlanId).Distinct())
            await context.TouchParentAsync(sampleId, cancellationToken);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
