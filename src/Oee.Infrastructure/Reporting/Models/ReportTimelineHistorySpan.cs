using Oee.Application.Contracts.Reporting;

namespace Oee.Infrastructure.Reporting;

internal sealed record ReportTimelineHistorySpan(Guid UnitId, EquipmentTimelineSpan Span);
