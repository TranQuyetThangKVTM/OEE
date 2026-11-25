
namespace Oee.Infrastructure.Reporting;

internal sealed record ReportTimelineStateEvent(Guid MappingId, Guid UnitId, DateTimeOffset At, string Kind, string Message);
