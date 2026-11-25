using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Downtime.Reasons.SetDowntimeReasonStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetDowntimeReasonStatus.</summary>
public sealed record SetDowntimeReasonStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
