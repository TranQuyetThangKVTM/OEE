
namespace Oee.Infrastructure.Reporting;

internal sealed record ReportTimelineShift(Guid SiteId, TimeOnly Start, TimeOnly End, string TimeZoneId);
