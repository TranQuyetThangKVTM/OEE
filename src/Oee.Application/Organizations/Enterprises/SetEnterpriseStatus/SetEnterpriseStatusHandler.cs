using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.Enterprises.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Enterprises.SetEnterpriseStatus;

/// <summary>Điều phối thao tác SetEnterpriseStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetEnterpriseStatusHandler(EnterpriseContext context)
    : IRequestHandler<SetEnterpriseStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetEnterpriseStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindEnterpriseAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Enterprise",
            async (entity, target) =>
            {
                if (target == RecordStatusEnum.Inactive
                    && await context.store.AnySitesAsync(
                    x => x.EnterpriseId == entity.Id && x.Status == RecordStatusEnum.Active,
                    cancellationToken))
                    throw ActiveDependencies("Enterprise", "active sites");
            },
            cancellationToken);
    }
}
