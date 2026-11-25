namespace Oee.Application.Contracts.Reporting;

public sealed record ReportBreakdownPoint(
    string Label,
    string? Category,
    int EventCount,
    decimal Value,
    decimal Percentage,
    decimal CumulativePercentage,
    string? DrillDownUrl = null);
