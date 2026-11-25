using Oee.Application.Common;

namespace Oee.Application.Downtime.Reasons.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteDowntimeReason.</summary>
public sealed record DeleteDowntimeReasonCommand(
    Guid Id,
    string RowVersion) : ICommand;
