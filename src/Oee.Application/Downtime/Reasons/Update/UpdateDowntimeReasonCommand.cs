using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Downtime.Reasons.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateDowntimeReason.</summary>
public sealed record UpdateDowntimeReasonCommand(
    Guid Id,
    string Name,
    string Category,
    string Kind,
    string? Description,
    string RowVersion
    )
    : ICommand<MasterMutationResult>;
