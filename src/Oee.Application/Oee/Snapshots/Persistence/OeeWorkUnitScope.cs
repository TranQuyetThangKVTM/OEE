namespace Oee.Application.Oee;

public sealed record OeeWorkUnitScope(
    Guid Id,
    Guid SiteId,
    string TimeZoneId);
