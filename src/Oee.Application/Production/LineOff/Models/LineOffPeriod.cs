namespace Oee.Application.Production.LineOff;

public sealed record LineOffPeriod(
    Guid Id,
    Guid WorkCenterId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Note,
    string TimeZoneId,
    string UpdatedBy,
    DateTimeOffset UpdatedAt,
    bool Deleted = false);
