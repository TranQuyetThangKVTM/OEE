using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.WorkCenters.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkCenters.SetWorkCenterStatus;

/// <summary>Điều phối thao tác SetWorkCenterStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetWorkCenterStatusHandler(WorkCenterContext context)
    : IRequestHandler<SetWorkCenterStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetWorkCenterStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindWorkCenterAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Work center",
            async (entity, target) =>
            {
                if (target == RecordStatusEnum.Active)
                    await MasterDataCommandContext.EnsureParentActiveAsync(
                        () => context.store.AnyAreasAsync(x => x.Id == entity.AreaId && x.Status == RecordStatusEnum.Active, cancellationToken),
                        entity.AreaId,
                        "Area");

                if (target == RecordStatusEnum.Inactive
                    && await context.store.AnyWorkUnitsAsync(
                    x => x.WorkCenterId == entity.Id && x.Status == RecordStatusEnum.Active,
                    cancellationToken))
                    throw ActiveDependencies("Work center", "active work units");
            },
            cancellationToken);
    }
}
