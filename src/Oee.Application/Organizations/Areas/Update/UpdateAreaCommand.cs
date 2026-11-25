using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Areas.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateArea.</summary>
public sealed record UpdateAreaCommand(
    Guid Id,
    string Name,
    string RowVersion,
    string? Description = null,
    string? Code = null
    )
    : ICommand<MasterMutationResult>;
