using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Shifts.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateShift.</summary>
public sealed record CreateShiftCommand(
    Guid SiteId,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Status
    )
    : ICommand<MasterMutationResult>;
