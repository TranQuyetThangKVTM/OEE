using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Areas.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateArea.</summary>
public sealed record CreateAreaCommand(
    Guid SiteId,
    string Code,
    string Name,
    string Status,
    string? Description = null
    )
    : ICommand<MasterMutationResult>;
