using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.WorkUnits.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateWorkUnit.</summary>
public sealed record UpdateWorkUnitCommand(
    Guid Id,
    string Name,
    string RowVersion,
    string? Description = null,
    string? Code = null
    )
    : ICommand<MasterMutationResult>;
