namespace Oee.Application.Contracts.Operations;

public sealed record CloseDowntimeRequest(
    DateTimeOffset EndAt,
    string ConcurrencyToken);
