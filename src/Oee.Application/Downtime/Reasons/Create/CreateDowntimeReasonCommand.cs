using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Downtime.Reasons.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateDowntimeReason.</summary>
public sealed record CreateDowntimeReasonCommand(
    string Code,
    string Name,
    string Category,
    string Kind,
    string? Description,
    string Status
    )
    : ICommand<MasterMutationResult>;
