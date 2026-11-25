namespace Oee.Application.Contracts.Reporting;

/// <summary>Kết quả Application đã tổng hợp cho cả Razor và PDF; Presentation không tính lại công thức.</summary>
public sealed record ReportData(
    ReportKind Kind,
    string Title,
    string FilterSummary,
    string TimeZoneId,
    DateTimeOffset GeneratedAt,
    IReadOnlyCollection<ReportMetric> Metrics,
    IReadOnlyCollection<ReportTrendPoint> Trend,
    IReadOnlyCollection<ReportBreakdownPoint> Breakdown,
    IReadOnlyCollection<ReportTableRow> Rows,
    int TotalCount,
    bool HasData,
    EquipmentTimelineData? Timeline = null);
