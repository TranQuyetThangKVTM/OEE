using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.WorkCenters.SetWorkCenterStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetWorkCenterStatus.</summary>
public sealed record SetWorkCenterStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
