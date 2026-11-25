using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.WorkCenters.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateWorkCenter.</summary>
public sealed record UpdateWorkCenterCommand(
    Guid Id,
    string Name,
    string RowVersion,
    string? Description = null,
    string? Code = null
    )
    : ICommand<MasterMutationResult>;
