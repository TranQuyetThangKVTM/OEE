using Oee.Domain.Common;

namespace Oee.Infrastructure.Reporting;

internal sealed record ReportDowntimeDbRaw(Guid Id, Guid WorkUnitId, string WorkUnit, Guid ReasonId, string ReasonCode,
    string Reason, DateTimeOffset Start, DateTimeOffset? End, DowntimeCategoryEnum Category, DowntimeKindEnum Kind,
    DataSourceEnum Source, string? Note);
