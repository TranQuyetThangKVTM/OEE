using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Web.Components.Pages;
using Oee.Web.Components.Pages.Dashboard.Components;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho Dashboard OEE, gồm dữ liệu Application Query, filter nhanh, drill-down và các UI state.
/// </summary>
public sealed class DashboardPageTests : BunitContext, IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 8, 2);

    /// <summary>Chuẩn bị MudBlazor/JS interop giả để component có thể render ngoài trình duyệt.</summary>
    public DashboardPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void DashboardRendersRealQueryResultAndDrillDownLinks()
    {
        // Arrange: fake Application Query trả DTO có KPI, trend và loss.
        var service = new FakeDashboardQueryService(CreateModel());
        Services.AddScoped<IDashboardQueryService>(_ => service);

        // Act: render trang Dashboard.
        var component = Render<Home>();

        // Assert: UI format tỷ lệ/sản lượng và sinh link drill-down từ dữ liệu DTO.
        component.WaitForAssertion(() =>
        {
            Assert.Contains(.82m.ToString("P1", CultureInfo.CurrentCulture), component.Markup, StringComparison.Ordinal);
            Assert.Contains("Tổng sản lượng", component.Markup, StringComparison.Ordinal);
            Assert.Contains(1_000m.ToString("N0", CultureInfo.CurrentCulture), component.Markup, StringComparison.Ordinal);
            Assert.Empty(component.FindAll("a[href='/live-oee']"));
            Assert.DoesNotContain("LIVE", component.Find(".oee-page-header").TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Trạng thái thiết bị", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("/reports/downtime-pareto", component.Markup, StringComparison.Ordinal);
            Assert.Equal(4, component.FindAll(".production-grid .metric-card").Count);
            Assert.Equal(4, component.FindAll(".kpi-grid .metric-card").Count);
            Assert.Empty(component.FindAll(".production-grid a, .production-grid button, .oee-card a, .oee-card button"));
            Assert.Empty(component.FindAll(".oee-card__head svg"));
        });

        Assert.NotNull(service.LastRequest);
        Assert.Equal(Today.AddDays(-6), service.LastRequest.FromDate);
        Assert.Equal(Today, service.LastRequest.ToDate);
    }

    [Fact]
    public void DashboardShowsFillerDayAndMonthSeparatelyFromAllMachineTotals()
    {
        var service = new FakeDashboardQueryService(CreateModel());
        Services.AddScoped<IDashboardQueryService>(_ => service);
        var component = Render<Home>();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("OEE toàn Line", component.Markup, StringComparison.Ordinal);
            Assert.Contains("M03 · Filler", component.Find(".line-oee-panel").TextContent, StringComparison.Ordinal);
            var gauges = component.FindComponents<LineOeeGauge>();
            Assert.Equal(2, gauges.Count);
            Assert.Equal(.9757m, gauges[0].Instance.Result!.Oee);
            Assert.Equal(.90m, gauges[1].Instance.Result!.Oee);
            Assert.Equal(.82m.ToString("P1", CultureInfo.CurrentCulture), component.Find(".kpi-grid .metric-card").QuerySelector(".metric-card__value")?.TextContent.Trim());
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public void LineGaugeDistinguishesMissingFromZeroAndFullOee(int? value)
    {
        decimal? oee = value;
        var result = new DashboardRollupResult(100, 100, 100, 100, 100, 0, 1, 1, 1, oee);
        var component = Render<LineOeeGauge>(parameters => parameters
            .Add(x => x.Title, "OEE ngày").Add(x => x.Period, "02/08/2026").Add(x => x.Result, result));

        Assert.Equal(oee.HasValue ? 2 : 1, component.FindAll(".mud-progress-circular").Count);
        Assert.Contains(oee?.ToString("0.##%", CultureInfo.CurrentCulture) ?? "—", component.Find("[role=img]").GetAttribute("aria-label"), StringComparison.Ordinal);
        Assert.Equal(!oee.HasValue, component.Markup.Contains("Chưa đủ dữ liệu", StringComparison.Ordinal));
    }

    [Fact]
    public void QuickRangeReloadsQueryWithThirtyDayWindow()
    {
        // Arrange: render dashboard với filter mặc định bảy ngày.
        var service = new FakeDashboardQueryService(CreateModel());
        Services.AddScoped<IDashboardQueryService>(_ => service);
        var component = Render<Home>();
        component.WaitForAssertion(() => Assert.Equal(1, service.QueryCount));

        // Act: bấm khoảng nhanh 30 ngày.
        component.FindAll("button").Single(button => button.TextContent.Trim() == "30 ngày").Click();

        // Assert: page gọi lại service đúng một lần với ngày bắt đầu mới.
        component.WaitForAssertion(() =>
        {
            Assert.Equal(2, service.QueryCount);
            Assert.Equal(Today.AddDays(-29), service.LastRequest!.FromDate);
            Assert.Equal(Today, service.LastRequest.ToDate);
        });
    }

    [Fact]
    public void QueryFailureKeepsRefreshActionWithoutTechnicalErrorBanner()
    {
        // Arrange: Application Query phát sinh lỗi database có kiểm soát.
        Services.AddScoped<IDashboardQueryService>(_ => new FakeDashboardQueryService(new InvalidOperationException("database unavailable")));

        var component = Render<Home>();

        // Assert: lỗi kỹ thuật không render thành banner; người dùng tải lại bằng PageHeader.
        component.WaitForAssertion(() =>
        {
            Assert.Contains("Làm mới", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("database unavailable", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void NoSnapshotOrProductionShowsEmptyState()
    {
        // Arrange: DTO hợp lệ nhưng HasData=false, không có trend/loss.
        var empty = CreateModel() with { HasData = false, Trend = [], Losses = [] };
        Services.AddScoped<IDashboardQueryService>(_ => new FakeDashboardQueryService(empty));

        var component = Render<Home>();

        // Assert: người dùng nhận empty state rõ ràng.
        component.WaitForAssertion(() => Assert.Contains("Chưa có dữ liệu OEE", component.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void PendingQueryShowsLoadingState()
    {
        // Arrange: TaskCompletionSource giữ Application Query ở trạng thái chưa hoàn tất.
        var pending = new TaskCompletionSource<DashboardModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        Services.AddScoped<IDashboardQueryService>(_ => new DelayedDashboardQueryService(pending.Task));

        // Act & Assert: loading xuất hiện trong lúc chờ và biến mất khi task hoàn thành.
        var component = Render<Home>();
        Assert.Contains("Đang tải dữ liệu", component.Markup, StringComparison.Ordinal);

        pending.SetResult(CreateModel());
        component.WaitForAssertion(() => Assert.DoesNotContain("Đang tải dữ liệu", component.Markup, StringComparison.Ordinal));
    }

    private static DashboardModel CreateModel()
    {
        // DTO mẫu chỉ tồn tại trong test; production page vẫn lấy dữ liệu qua IDashboardQueryService.
        return new DashboardModel(
            new(.82m, .85m, .80m, .02m),
            new(.90m, .90m, .88m, .02m),
            new(.92m, .95m, .91m, .01m),
            new(.99m, .99m, .98m, .01m),
            new(1_000m, 990m, 10m, 1_200m),
            new(480m, 432m, 397.44m),
            [new(Today, .82m, .90m, .92m, .99m)],
            [new("DR-001", "Mất áp suất", "Failure", 2, 15m, 1m)],
            "SE Asia Standard Time",
            true);
    }

    private sealed class FakeDashboardQueryService : IDashboardQueryService
    {
        private readonly DashboardModel? model;
        private readonly Exception? exception;

        public FakeDashboardQueryService(DashboardModel model) => this.model = model;
        public FakeDashboardQueryService(Exception exception) => this.exception = exception;

        public DashboardQueryRequest? LastRequest { get; private set; }
        public int QueryCount { get; private set; }

        public Task<LineOeeSummary> GetLineOeeAsync(DashboardQueryRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LineOeeSummary(request.ToDate, "LINE1", "M03 · Filler", "UTC",
                new(100, 100, 100, 100, 100, 0, 1, 1, 1, .9757m),
                new(200, 200, 180, 180, 180, 0, 1, .90m, 1, .90m), null));

        public Task<DashboardFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken) => Task.FromResult(
            new DashboardFilterOptions(Today, [], [], [], [], [], []));

        public Task<DashboardModel> GetAsync(DashboardQueryRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            QueryCount++;
            return exception is null ? Task.FromResult(model!) : Task.FromException<DashboardModel>(exception);
        }
    }

    private sealed class DelayedDashboardQueryService(Task<DashboardModel> result) : IDashboardQueryService
    {
        public Task<LineOeeSummary> GetLineOeeAsync(DashboardQueryRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LineOeeSummary(request.ToDate, null, null, null, null, null, null));

        public Task<DashboardFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken) => Task.FromResult(
            new DashboardFilterOptions(Today, [], [], [], [], [], []));

        public Task<DashboardModel> GetAsync(DashboardQueryRequest request, CancellationToken cancellationToken) =>
            result.WaitAsync(cancellationToken);
    }
}
