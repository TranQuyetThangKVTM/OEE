using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.WorkUnits.SetWorkUnitStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetWorkUnitStatus.</summary>
public sealed record SetWorkUnitStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
