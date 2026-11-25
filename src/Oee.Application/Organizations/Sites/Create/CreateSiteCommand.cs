using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Sites.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateSite.</summary>
public sealed record CreateSiteCommand(
    Guid EnterpriseId,
    string Code,
    string Name,
    string TimeZoneId,
    string Status,
    string? Description = null
    )
    : ICommand<MasterMutationResult>;
