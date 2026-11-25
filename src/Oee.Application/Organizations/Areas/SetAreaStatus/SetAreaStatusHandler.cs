using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.Areas.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Areas.SetAreaStatus;

/// <summary>Điều phối thao tác SetAreaStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetAreaStatusHandler(AreaContext context)
    : IRequestHandler<SetAreaStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetAreaStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindAreaAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Area",
            async (entity, target) =>
            {
                if (target == RecordStatusEnum.Active)
                    await MasterDataCommandContext.EnsureParentActiveAsync(
                        () => context.store.AnySitesAsync(x => x.Id == entity.SiteId && x.Status == RecordStatusEnum.Active, cancellationToken),
                        entity.SiteId,
                        "Site");

                if (target == RecordStatusEnum.Inactive
                    && await context.store.AnyWorkCentersAsync(
                    x => x.AreaId == entity.Id && x.Status == RecordStatusEnum.Active,
                    cancellationToken))
                    throw ActiveDependencies("Area", "active work centers");
            },
            cancellationToken);
    }
}
