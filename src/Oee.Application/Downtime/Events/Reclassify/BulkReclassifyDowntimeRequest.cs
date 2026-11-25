namespace Oee.Application.Contracts.Operations;

public sealed record BulkReclassifyDowntimeRequest(
    IReadOnlyCollection<BulkReclassifyDowntimeItem> Items,
    Guid DowntimeReasonId);
