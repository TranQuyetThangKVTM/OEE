using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Sites.SetSiteStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetSiteStatus.</summary>
public sealed record SetSiteStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
