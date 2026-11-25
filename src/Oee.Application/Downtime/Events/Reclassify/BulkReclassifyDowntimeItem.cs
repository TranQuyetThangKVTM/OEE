namespace Oee.Application.Contracts.Operations;

public sealed record BulkReclassifyDowntimeItem(
    Guid DowntimeId,
    string ConcurrencyToken);
