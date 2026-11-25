using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Sites.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateSite.</summary>
public sealed record UpdateSiteCommand(
    Guid Id,
    string Name,
    string TimeZoneId,
    string RowVersion,
    string? Description = null,
    string? Code = null
    )
    : ICommand<MasterMutationResult>;
