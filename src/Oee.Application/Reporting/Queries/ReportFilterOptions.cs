namespace Oee.Application.Contracts.Reporting;

public sealed record ReportFilterOptions(
    DateOnly Today,
    IReadOnlyCollection<ReportFilterOption> Sites,
    IReadOnlyCollection<ReportFilterOption> Areas,
    IReadOnlyCollection<ReportFilterOption> WorkCenters,
    IReadOnlyCollection<ReportFilterOption> WorkUnits,
    IReadOnlyCollection<ReportFilterOption> Products,
    IReadOnlyCollection<ReportFilterOption> Shifts,
    IReadOnlyCollection<ReportFilterOption> DowntimeReasons,
    DateTimeOffset? GeneratedAt = null);
