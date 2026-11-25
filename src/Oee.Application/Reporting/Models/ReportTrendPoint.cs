namespace Oee.Application.Contracts.Reporting;

public sealed record ReportTrendPoint(
    string Label,
    decimal? Oee,
    decimal? Availability,
    decimal? Performance,
    decimal? Quality,
    decimal Total,
    decimal Good,
    decimal Defect,
    decimal DowntimeMinutes);
