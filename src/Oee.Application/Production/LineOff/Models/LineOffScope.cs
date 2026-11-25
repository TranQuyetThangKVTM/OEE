namespace Oee.Application.Production.LineOff;

public sealed record LineOffScope(Guid WorkCenterId, Guid FillerId, string TimeZoneId);
