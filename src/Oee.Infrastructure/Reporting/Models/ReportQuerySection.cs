using Oee.Application.Contracts.Reporting;

namespace Oee.Infrastructure.Reporting;

internal sealed record ReportQuerySection(IReadOnlyCollection<ReportMetric> Metrics, IReadOnlyCollection<ReportTrendPoint> Trend,
    IReadOnlyCollection<ReportBreakdownPoint> Breakdown, IReadOnlyCollection<ReportTableRow> Rows, int TotalCount, bool HasData,
    EquipmentTimelineData? Timeline = null)
{
    public static ReportQuerySection Empty { get; } = new([], [], [], [], 0, false);
}
