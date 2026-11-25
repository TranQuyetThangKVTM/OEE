using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Shifts.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateShift.</summary>
public sealed record UpdateShiftCommand(
    Guid Id,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string RowVersion
    )
    : ICommand<MasterMutationResult>;
