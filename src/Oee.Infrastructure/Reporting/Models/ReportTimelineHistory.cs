
namespace Oee.Infrastructure.Reporting;

internal sealed record ReportTimelineHistory(IReadOnlyList<ReportTimelineHistorySpan> Spans, HashSet<Guid> Units);
