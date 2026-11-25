using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.Sites.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Sites.SetSiteStatus;

/// <summary>Điều phối thao tác SetSiteStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetSiteStatusHandler(SiteContext context)
    : IRequestHandler<SetSiteStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetSiteStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindSiteAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Site",
            async (entity, target) =>
            {
                if (target == RecordStatusEnum.Active)
                    await MasterDataCommandContext.EnsureParentActiveAsync(
                        () => context.store.AnyEnterprisesAsync(
                            x => x.Id == entity.EnterpriseId && x.Status == RecordStatusEnum.Active,
                            cancellationToken),
                        entity.EnterpriseId,
                        "Enterprise");

                if (target == RecordStatusEnum.Inactive
                    && (await context.store.AnyAreasAsync(x => x.SiteId == entity.Id && x.Status == RecordStatusEnum.Active, cancellationToken)
                    || await context.store.AnyShiftsAsync(x => x.SiteId == entity.Id && x.Status == RecordStatusEnum.Active, cancellationToken)))
                    throw ActiveDependencies("Site", "active areas or shifts");
            },
            cancellationToken);
    }
}
