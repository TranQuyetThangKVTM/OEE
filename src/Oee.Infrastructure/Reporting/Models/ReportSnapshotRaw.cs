using Oee.Domain.Common;

namespace Oee.Infrastructure.Reporting;

internal sealed record ReportSnapshotRaw(Guid WorkUnitId, string WorkUnit, string PeriodCode, OeePeriodTypeEnum PeriodType,
    DateTimeOffset Start, DateTimeOffset End, decimal Loading, decimal Utilization, decimal NetUtilization,
    decimal Total, decimal Defect);
