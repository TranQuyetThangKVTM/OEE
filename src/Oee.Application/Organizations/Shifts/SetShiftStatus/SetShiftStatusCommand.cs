using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Shifts.SetShiftStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetShiftStatus.</summary>
public sealed record SetShiftStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
