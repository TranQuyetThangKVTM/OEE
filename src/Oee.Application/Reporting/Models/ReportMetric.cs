namespace Oee.Application.Contracts.Reporting;

/// <summary>KPI báo cáo; IsRatio=true nghĩa Value lưu 0..1 và formatter hiển thị phần trăm.</summary>
public sealed record ReportMetric(
    string Key,
    string Label,
    decimal? Value,
    string Unit,
    bool IsRatio = false,
    string? Description = null);
