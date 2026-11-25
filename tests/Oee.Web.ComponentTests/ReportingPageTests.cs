using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;
using Oee.Web.Components.Pages.Reports;
using Oee.Web.Components.Shared;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho ReportWorkspace dùng chung: filter, KPI/chart/table, PDF URL, state và chi tiết thiết bị.
/// </summary>
public sealed class ReportingPageTests : BunitContext, IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 8, 2);
    private static readonly Guid UnitId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly string[] ExpectedPeriodLabels = ["2026-W40", "2026-W42"];
    private static readonly double[] ExpectedPeriodOee = [80, 85];

    /// <summary>Chuẩn bị Viewer mặc định cho các kiểm tra báo cáo.</summary>
    public ReportingPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("viewer");
        authorization.SetRoles("Viewer");    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void DeviceReportRendersQueryMetricsChartsRowsAndCurrentFilterPdfUrl()
    {
        // Arrange: Application Query giả trả một ReportData có metric, trend, breakdown và row.
        var query = new FakeReportQuery(CreateReport(ReportKind.DeviceOee));
        Services.AddScoped<IReportAnalysisQueryService>(_ => query);

        // Act: render workspace cho loại DeviceOee.
        var component = Render<ReportWorkspace>(parameters => parameters
            .Add(x => x.Kind, ReportKind.DeviceOee).Add(x => x.Title, "Báo cáo OEE"));

        // Assert: UI/PDF dùng cùng filter hiện tại và tạo drill-down theo WorkUnit.
        component.WaitForAssertion(() =>
        {
            Assert.Contains(.82m.ToString("P1", System.Globalization.CultureInfo.CurrentCulture), component.Markup, StringComparison.Ordinal);
            Assert.Contains("M-001", component.Markup, StringComparison.Ordinal);
            Assert.Contains("DR-001", component.Markup, StringComparison.Ordinal);
            Assert.Contains("/reports/device-detail?workUnitId=", component.Markup, StringComparison.Ordinal);
            Assert.Contains("/api/v1/reports/DeviceOee/pdf?", component.Markup, StringComparison.Ordinal);
            Assert.Contains("fromDate=2026-07-04", component.Markup, StringComparison.Ordinal);
        });
        Assert.NotNull(query.LastQuery);
        Assert.Equal(1, query.LastQuery.Page);
        Assert.Equal(25, query.LastQuery.PageSize);
    }

    [Fact]
    public void EmptyResultHasExplicitState()
    {
        // Arrange: query hoàn tất hợp lệ nhưng không có dòng theo bộ lọc.
        Services.AddScoped<IReportAnalysisQueryService>(_ => new FakeReportQuery(CreateReport(ReportKind.DailyOee) with
        {
            Metrics = [],
            Trend = [],
            Breakdown = [],
            Rows = [],
            TotalCount = 0,
            HasData = false,
        }));
        var empty = Render<ReportWorkspace>(parameters => parameters
            .Add(x => x.Kind, ReportKind.DailyOee).Add(x => x.Title, "KPI ngày"));
        // Assert: hiển thị empty state rõ ràng thay vì bảng trắng.
        empty.WaitForAssertion(() => Assert.Contains("Không có dữ liệu phù hợp với bộ lọc", empty.Markup, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Máy A&B + SKU/01? #1=100%")]
    [InlineData("   ")]
    [InlineData(null)]
    public void PdfLinkPreservesSearchAndDeviceFiltersWithoutExtraQueryParameters(string? search)
    {
        var query = new FakeReportQuery(CreateReport(ReportKind.DeviceOee));
        Services.AddScoped<IReportAnalysisQueryService>(_ => query);
        var component = Render<ReportWorkspace>(parameters => parameters
            .Add(x => x.Kind, ReportKind.DeviceOee)
            .Add(x => x.InitialWorkUnitId, UnitId)
            .Add(x => x.InitialSearch, search));

        component.WaitForAssertion(() =>
        {
            var href = component.Find("a[href*='/api/v1/reports/DeviceOee/pdf']").GetAttribute("href")!;
            var uri = new Uri(new Uri("http://localhost"), href);
            var filters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            Assert.Equal(UnitId.ToString(), filters["workUnitId"].ToString());
            Assert.Equal("2026-07-04", filters["fromDate"].ToString());
            Assert.Equal("2026-08-02", filters["toDate"].ToString());
            Assert.Empty(uri.Fragment);
            Assert.DoesNotContain("productId", filters.Keys);

            if (string.IsNullOrWhiteSpace(search))
                Assert.DoesNotContain("search", filters.Keys);
            else
            {
                Assert.Equal(search, filters["search"].ToString());
                Assert.Equal(search, query.LastQuery?.Search);
            }
        });
    }

    [Fact]
    public void QueryFailureKeepsPageHeaderRefreshWithoutTechnicalErrorBanner()
    {
        // Arrange & Act: query ném lỗi database để kiểm tra error boundary cấp page.
        Services.AddScoped<IReportAnalysisQueryService>(_ => new FakeReportQuery(new InvalidOperationException("database unavailable")));
        var failed = Render<ReportWorkspace>(parameters => parameters
            .Add(x => x.Kind, ReportKind.DailyOee).Add(x => x.Title, "KPI ngày"));
        // Assert: PageHeader giữ nút Làm mới và không lộ exception hạ tầng.
        failed.WaitForAssertion(() =>
        {
            Assert.Contains("Làm mới", failed.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("database unavailable", failed.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void DeviceDetailStartsWithRequestedDeviceScope()
    {
        // Arrange: truyền WorkUnitId từ drill-down route vào parameter của workspace.
        var query = new FakeReportQuery(CreateReport(ReportKind.DeviceOee));
        Services.AddScoped<IReportAnalysisQueryService>(_ => query);
        var component = Render<ReportWorkspace>(parameters => parameters.Add(x => x.Kind, ReportKind.DeviceOee)
            .Add(x => x.Title, "Chi tiết")
            .Add(x => x.InitialWorkUnitId, UnitId));
        // Assert: request đầu tiên đã scope đúng thiết bị, không tải toàn fleet rồi lọc ở browser.
        component.WaitForAssertion(() => Assert.Equal(UnitId, query.LastQuery?.WorkUnitId));
    }

    [Theory]
    [InlineData(ReportKind.ShiftKpi, "Ca sản xuất", "2026-07-20")]
    [InlineData(ReportKind.DailyOee, "Tổng SL", "2026-07-20")]
    [InlineData(ReportKind.WeeklyKpi, "Khoảng ngày", "2026-06-08")]
    [InlineData(ReportKind.MonthlyKpi, "Trạng thái", "2025-08-02")]
    public void KpiPagesHaveFocusedMudFiltersAndPeriodSpecificTables(ReportKind kind, string heading, string from)
    {
        Services.AddScoped<IReportAnalysisQueryService>(_ => new FakeReportQuery(CreateReport(kind)));
        var component = Render<ReportWorkspace>(p => p.Add(x => x.Kind, kind).Add(x => x.Title, "KPI"));
        component.WaitForAssertion(() =>
        {
            Assert.Contains(heading, component.Markup, StringComparison.Ordinal);
            Assert.Contains("fromDate=" + from, component.Markup, StringComparison.Ordinal);
        });
        Assert.Single(component.FindComponents<MudDateRangePicker>());
        Assert.Empty(component.FindAll("input[type=date]"));
        Assert.DoesNotContain("Nhà máy", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Phân bố / Pareto", component.Markup, StringComparison.Ordinal);
        var query = Services.GetRequiredService<IReportAnalysisQueryService>() as FakeReportQuery;
        Assert.Equal(kind is not (ReportKind.WeeklyKpi or ReportKind.MonthlyKpi), query?.LastQuery?.Descending);
        if (kind is ReportKind.ShiftKpi or ReportKind.DailyOee)
            Assert.Empty(component.FindComponents<KpiReportSummary>());
        else Assert.Single(component.FindComponents<KpiReportSummary>());
    }

    [Fact]
    public void PeriodChartUsesOrderedPeriodsAndDoesNotInventZeroForMissingRatios()
    {
        var data = CreateReport(ReportKind.WeeklyKpi) with
        {
            Trend = [
                new("2026-W40", .8m, .9m, .9m, .99m, 100, 99, 1, 10),
                new("2026-W41", null, null, null, null, 0, 0, 0, 0),
                new("2026-W42", .85m, .95m, .9m, .99m, 100, 99, 1, 10)
            ]
        };
        var component = Render<KpiReportSummary>(p => p.Add(x => x.Data, data));
        var chart = component.FindComponent<MudChart<double>>().Instance;
        Assert.Equal(ExpectedPeriodLabels, chart.ChartLabels);
        Assert.Equal(ExpectedPeriodOee, chart.ChartSeries.Single(x => x.Name == "OEE").Data);
        Assert.Contains("1 / 3 tuần", component.Markup, StringComparison.Ordinal);
        Assert.Contains("điểm %", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ReliabilityHasHumanDurationsUptimeTableAndTimelineWithoutGenericCharts()
    {
        var start = new DateTimeOffset(2026, 8, 2, 6, 0, 0, TimeSpan.FromHours(7));
        var timeline = new EquipmentTimelineData(start, start.AddHours(8), start.AddHours(8), "Asia/Ho_Chi_Minh",
            [new(UnitId, "M-001", "Machine", [new(start, start.AddHours(8), EquipmentTimelineState.Running)], 480, 0, 0, 0)]);
        var data = CreateReport(ReportKind.Reliability) with
        {
            Metrics = [new("availability", "Availability", 1, "%", true),
                new("mtbf", "MTBF", 1440, "phút"), new("mttr", "MTTR", .3m, "phút"),
                new("failures", "Số sự cố", 1, "sự kiện")],
            Rows = [new(UnitId, "Machine", UnitId, "M-001", Code: "M-001", Availability: 1,
                EventCount: 1, MtbfMinutes: 1440, MttrMinutes: .3m, UptimeMinutes: 480)],
            Timeline = timeline
        };
        Services.AddScoped<IReportAnalysisQueryService>(_ => new FakeReportQuery(data));
        var component = Render<ReportWorkspace>(p => p.Add(x => x.Kind, ReportKind.Reliability).Add(x => x.Title, "Độ tin cậy"));
        component.WaitForAssertion(() =>
        {
            Assert.Contains("18 giây", component.Markup, StringComparison.Ordinal);
            Assert.Contains("8h00", component.Markup, StringComparison.Ordinal);
            Assert.Single(component.FindComponents<EquipmentTimelineView>());
        });
        Assert.Empty(component.FindComponents<MudChart<double>>());
        Assert.Single(component.FindComponents<MudDateRangePicker>());
        Assert.DoesNotContain("Phân bố / Pareto", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TrackingSegmentOpensReadOnlyDetailWithReasonAndTimes()
    {
        var start = new DateTimeOffset(2026, 8, 2, 14, 0, 0, TimeSpan.FromHours(7));
        var data = new EquipmentTimelineData(start, start.AddHours(8), start.AddHours(8), "Asia/Ho_Chi_Minh",
            [new(UnitId, "M1", "Machine", [new(start, start.AddSeconds(18), EquipmentTimelineState.Failure,
                Guid.NewGuid(), "Kẹt chai", "Đã kiểm tra")], 0, .3m, 1, 1)]);
        var provider = Render<MudDialogProvider>();
        var component = Render<EquipmentTimelineView>(p => p.Add(x => x.Data, data));
        component.Find(".equipment-segment--Failure button").Click();
        provider.WaitForAssertion(() =>
        {
            Assert.Contains("Kẹt chai", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("18 giây", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("14:00:18", provider.Markup, StringComparison.Ordinal);
        });
        Assert.DoesNotContain("Lưu", provider.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DowntimeRouteDoesNotSendLiteralSearchAsFilter()
    {
        var query = new FakeReportQuery(CreateReport(ReportKind.DowntimeEvents));
        Services.AddScoped<IReportAnalysisQueryService>(_ => query);
        var component = Render<DowntimeEventsReport>();
        component.WaitForAssertion(() => Assert.NotNull(query.LastQuery));
        Assert.Null(Assert.IsType<ReportQuery>(query.LastQuery).Search);
    }

    [Theory]
    [InlineData(ReportKind.ShiftKpi)]
    [InlineData(ReportKind.DailyOee)]
    [InlineData(ReportKind.DowntimeEvents)]
    [InlineData(ReportKind.DeviceOee)]
    public async Task ReportRowsCanGroupByMachineWithoutChangingPdfFilters(ReportKind kind)
    {
        Services.AddScoped<IReportAnalysisQueryService>(_ => new FakeReportQuery(CreateReport(kind)));
        var page = Render<ReportWorkspace>(p => p.Add(x => x.Kind, kind).Add(x => x.Title, "Báo cáo"));
        page.WaitForAssertion(() => Assert.Single(page.FindComponents<TableGroupingSelector>()));
        var exportBefore = page.FindAll("a").Single(a => a.TextContent.Contains("Xuất PDF", StringComparison.Ordinal)).GetAttribute("href");
        var selector = page.FindComponent<TableGroupingSelector>();
        Assert.False(selector.Instance.AllowProduct);
        await page.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(TableGroupingMode.Machine));
        page.WaitForAssertion(() => Assert.Contains("M-001", page.Find(".oee-table-group").TextContent, StringComparison.Ordinal));
        Assert.Equal(exportBefore, page.FindAll("a").Single(a => a.TextContent.Contains("Xuất PDF", StringComparison.Ordinal)).GetAttribute("href"));
    }

    [Theory]
    [InlineData(ReportKind.WeeklyKpi)]
    [InlineData(ReportKind.MonthlyKpi)]
    [InlineData(ReportKind.Reliability)]
    [InlineData(ReportKind.QualityLoss)]
    public void AlreadyAggregatedReportsDoNotOfferArtificialGrouping(ReportKind kind)
    {
        Services.AddScoped<IReportAnalysisQueryService>(_ => new FakeReportQuery(CreateReport(kind)));
        var page = Render<ReportWorkspace>(p => p.Add(x => x.Kind, kind).Add(x => x.Title, "Báo cáo"));
        page.WaitForAssertion(() => Assert.Single(page.FindComponents<Oee.Web.Components.Shared.PageTable<ReportTableRow>>()));
        Assert.Empty(page.FindComponents<TableGroupingSelector>());
    }

    private static ReportData CreateReport(ReportKind kind) => new(kind, "Báo cáo OEE",
        "04/07/2026 – 02/08/2026", "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow,
        [new("oee", "OEE", .82m, "%", true), new("total", "Tổng", 100, "sp")],
        [new("02/08", .82m, .9m, .92m, .99m, 100, 99, 1, 10)],
        [new("DR-001 · Breakdown", "AvailabilityLoss", 1, 10, 1, 1, "/reports/downtime-events")],
        [new(null, "M-001", UnitId, "M-001", Oee: .82m, Availability: .9m, Performance: .92m,
            Quality: .99m, Total: 100, Good: 99, Defect: 1,
            DrillDownUrl: "/reports/device-detail?workUnitId=" + UnitId)], 1, true);

    private static ReportFilterOptions FilterOptions() => new(Today,
        [new(Guid.NewGuid(), "SITE-01", "Factory", Detail: "Asia/Ho_Chi_Minh")],
        [new(Guid.NewGuid(), "PROC-01", "Area")],
        [new(Guid.NewGuid(), "LINE-01", "Line")],
        [new(UnitId, "M-001", "Machine")],
        [new(Guid.NewGuid(), "SKU-001", "Product")],
        [new(Guid.NewGuid(), "S3", "Night")],
        [new(Guid.NewGuid(), "DR-001", "Breakdown")]);

    private sealed class FakeReportQuery : IReportAnalysisQueryService
    {
        private readonly ReportData? data; private readonly Exception? exception;
        public FakeReportQuery(ReportData data) => this.data = data;
        public FakeReportQuery(Exception exception) => this.exception = exception;
        public ReportQuery? LastQuery { get; private set; }
        public Task<ReportFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(FilterOptions());
        public Task<ReportData> QueryAsync(ReportQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return exception is null ? Task.FromResult(data!) : Task.FromException<ReportData>(exception);
        }
    }

}
