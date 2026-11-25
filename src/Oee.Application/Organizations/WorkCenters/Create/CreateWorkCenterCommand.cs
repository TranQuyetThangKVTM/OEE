using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.WorkCenters.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateWorkCenter.</summary>
public sealed record CreateWorkCenterCommand(
    Guid AreaId,
    string Code,
    string Name,
    string Status,
    string? Description = null
    )
    : ICommand<MasterMutationResult>;
