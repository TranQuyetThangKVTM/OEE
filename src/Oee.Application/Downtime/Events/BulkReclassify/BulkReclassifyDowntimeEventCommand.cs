using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Downtime.Events.BulkReclassify;

/// <summary>Dữ liệu yêu cầu cho thao tác BulkReclassifyDowntimeEvent.</summary>
public sealed record BulkReclassifyDowntimeEventCommand(
    IReadOnlyCollection<BulkReclassifyDowntimeItem> Requests,
    Guid ReasonId)
    : ICommand<int>;
