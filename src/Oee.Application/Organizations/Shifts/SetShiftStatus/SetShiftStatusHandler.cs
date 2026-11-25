using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.Shifts.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Shifts.SetShiftStatus;

/// <summary>Điều phối thao tác SetShiftStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetShiftStatusHandler(ShiftContext context)
    : IRequestHandler<SetShiftStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetShiftStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindShiftAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Shift",
            async (entity, target) =>
            {
                if (target == RecordStatusEnum.Active)
                    await MasterDataCommandContext.EnsureParentActiveAsync(
                        () => context.store.AnySitesAsync(x => x.Id == entity.SiteId && x.Status == RecordStatusEnum.Active, cancellationToken),
                        entity.SiteId,
                        "Site");

                if (target == RecordStatusEnum.Inactive
                    && (await context.store.AnyCurrentProductionPlansAsync(x => x.ShiftId == entity.Id, cancellationToken)
                    || await context.store.AnySampleProductionPlanItemsAsync(x => x.ShiftId == entity.Id, cancellationToken)))
                    throw ActiveDependencies("Shift", "current production plans or sample-plan lines");
            },
            cancellationToken);
    }
}
