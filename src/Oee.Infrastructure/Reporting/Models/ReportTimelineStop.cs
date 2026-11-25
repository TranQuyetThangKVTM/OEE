using Oee.Domain.Common;

namespace Oee.Infrastructure.Reporting;

internal sealed record ReportTimelineStop(Guid Id, Guid UnitId, DateTimeOffset Start, DateTimeOffset? End,
    DowntimeKindEnum Kind, DowntimeCategoryEnum Category, string Reason, string? Note);
