using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Reasons.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Downtime.Reasons.SetDowntimeReasonStatus;

/// <summary>Điều phối thao tác SetDowntimeReasonStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetDowntimeReasonStatusHandler(DowntimeReasonContext context)
    : IRequestHandler<SetDowntimeReasonStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetDowntimeReasonStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindDowntimeReasonAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Downtime reason",
            async (entity, target) =>
            {
                if (entity.Code == "DR-UNKNOWN" && target != RecordStatusEnum.Active)
                    throw new InvalidOperationException("DR-UNKNOWN is a protected active system reason.");

                if (target == RecordStatusEnum.Inactive
                    && (await context.store.AnyDowntimeEventsAsync(x => x.DowntimeReasonId == entity.Id && x.EndAt == null, cancellationToken)
                    || await context.store.AnyPlcFaultCodeMappingsAsync(x => x.DowntimeReasonId == entity.Id && x.IsActive, cancellationToken)))
                    throw ActiveDependencies("Downtime reason", "open downtime or active PLC fault mappings");
            },
            cancellationToken);
    }
}
