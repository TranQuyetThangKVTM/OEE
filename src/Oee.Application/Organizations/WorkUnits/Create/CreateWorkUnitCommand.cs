using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.WorkUnits.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateWorkUnit.</summary>
public sealed record CreateWorkUnitCommand(
    Guid WorkCenterId,
    string Code,
    string Name,
    string Status,
    string? Description = null
    )
    : ICommand<MasterMutationResult>;
