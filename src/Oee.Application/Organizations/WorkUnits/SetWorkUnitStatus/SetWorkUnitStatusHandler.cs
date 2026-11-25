using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.WorkUnits.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkUnits.SetWorkUnitStatus;

/// <summary>Điều phối thao tác SetWorkUnitStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetWorkUnitStatusHandler(WorkUnitContext context)
    : IRequestHandler<SetWorkUnitStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetWorkUnitStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindWorkUnitAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Work unit",
            async (entity, target) =>
            {
                if (target == RecordStatusEnum.Active)
                    await MasterDataCommandContext.EnsureParentActiveAsync(
                        () => context.store.AnyWorkCentersAsync(
                            x => x.Id == entity.WorkCenterId && x.Status == RecordStatusEnum.Active,
                            cancellationToken),
                        entity.WorkCenterId,
                        "Work center");

                if (target == RecordStatusEnum.Inactive
                    && (await context.store.AnyCurrentProductionPlansAsync(x => x.WorkUnitId == entity.Id, cancellationToken)
                    || await context.store.AnyDowntimeEventsAsync(x => x.WorkUnitId == entity.Id && x.EndAt == null, cancellationToken)))
                    throw ActiveDependencies("Work unit", "current production plans or open downtime");
            },
            cancellationToken);
    }
}
