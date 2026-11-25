using Oee.Application.Common;
using Oee.Application.Organizations.WorkUnits.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkUnits.Delete;

/// <summary>Điều phối thao tác DeleteWorkUnit trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteWorkUnitHandler(WorkUnitContext context) : ICommandHandler<DeleteWorkUnitCommand>
{
    public async Task HandleAsync(DeleteWorkUnitCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindWorkUnitAsync(id, cancellationToken) ?? throw Missing("Work unit");

        if (await context.store.AnyProductionPlansAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyProductionOutputsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyProductionReconciliationItemsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyDowntimeEventsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyWorkUnitStandardsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnySampleProductionPlanItemsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyOeeSnapshotsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyOeeTargetsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyPlcDeviceMappingsAsync(x => x.WorkUnitId == id, cancellationToken)
            || await context.store.AnyPlcFaultCodeMappingsAsync(x => x.WorkUnitId == id, cancellationToken))
            throw Referenced("Work unit");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
