namespace Oee.Application.Contracts.Operations;

public sealed record ReclassifyDowntimeRequest(
    Guid DowntimeReasonId,
    string ConcurrencyToken);
