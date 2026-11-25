using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Reporting;
using ChartSeries = MudBlazor.ChartSeries<double>;

namespace Oee.Web.Components.Pages.Reports;

public partial class KpiReportSummary
{
    [Parameter, EditorRequired]
    public ReportData Data { get; set; } = default!;

    private List<ChartSeries> series = [];
    private string[] labels = [];
    private readonly LineChartOptions lineOptions = new()
    {
        ChartPalette = ["#3285ff", "#13bfa5", "#f5bd00", "#f08b17"],
        YAxisFormat = "0'%'",
        LineStrokeWidth = 3,
        YAxisTicks = 20,
        ShowDataMarkers = true,
        YAxisSuggestedMax = 100
    };
    private string Period => Data.Kind == ReportKind.WeeklyKpi ? "tuần" : Data.Kind == ReportKind.MonthlyKpi ? "tháng" : "ngày";
    private string ChartTitle => "Xu hướng OEE theo " + Period;

    private decimal? Change
    {
        get
        {
            var points = Data.Trend.Where(x => x.Oee.HasValue).ToArray();
            return points.Length < 2 ? null : (points[^1].Oee - points[^2].Oee) * 100;
        }
    }

    private string ChangeText => Change.HasValue ? (Change >= 0 ? "▲ +" : "▼ ") + Change.Value.ToString("N1", CultureInfo.CurrentCulture) + " điểm %" : "Chưa đủ kỳ để so sánh";

    protected override void OnParametersSet()
    {
        // Missing ratios remain unknown; do not turn them into zero-value chart points.
        var points = Data.Trend.Where(x => x.Oee.HasValue && x.Availability.HasValue && x.Performance.HasValue && x.Quality.HasValue).ToArray();
        labels = points.Select(x => x.Label).ToArray();
        series = [];
        if (points.Length == 0)
            return;
        series = [new()
        {
            Name = "Sẵn sàng",
            Data = points.Select(x => (double)(x.Availability!.Value * 100)).ToArray()
        }, new()
        {
            Name = "Hiệu suất",
            Data = points.Select(x => (double)(x.Performance!.Value * 100)).ToArray()
        }, new()
        {
            Name = "Chất lượng",
            Data = points.Select(x => (double)(x.Quality!.Value * 100)).ToArray()
        }, new()
        {
            Name = "OEE",
            Data = points.Select(x => (double)(x.Oee!.Value * 100)).ToArray()
        }

        ];
    }

    private string Description(ReportMetric metric) => metric.Description ?? (metric.Key == "good" ? $"Đạt mục tiêu: {Data.Trend.Count(x => x.Oee >= .85m)} / {Data.Trend.Count} {Period}" : $"{Data.Trend.Count} {Period} · mục tiêu 85%");
    private static string Format(ReportMetric metric) => metric.Key is "mtbf" or "mttr" ? EquipmentTimeText.Duration(metric.Value) : !metric.Value.HasValue ? "—" : metric.Value.Value.ToString(metric.IsRatio ? "P1" : "N0", CultureInfo.CurrentCulture);
    private static string MetricIcon(string key) => key switch
    {
        "oee-high" => Icons.Material.Filled.TrendingUp,
        "oee-low" => Icons.Material.Filled.TrendingDown,
        "good" => Icons.Material.Filled.Inventory2,
        "mtbf" or "mttr" => Icons.Material.Filled.BuildCircle,
        "failures" => Icons.Material.Filled.Warning,
        _ => Icons.Material.Filled.Speed
    };
}
