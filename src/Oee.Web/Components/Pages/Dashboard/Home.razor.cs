using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using ChartSeries = MudBlazor.ChartSeries<double>;

namespace Oee.Web.Components.Pages;

public partial class Home
{
    // lifetime được hủy khi rời route để dừng query/poll; loading/error/model điều khiển bốn trạng thái UI.
    private DashboardFilterOptions? filters;
    private DashboardModel? model;
    private LineOeeSummary? lineOee;
    private CancellationTokenSource lifetime = new();
    private bool loading = true;
    private string? error;
    private DateOnly fromDate;
    private DateOnly toDate;
    private int activeRangeDays = 7;
    private Guid? siteId;
    private Guid? areaId;
    private Guid? workCenterId;
    private Guid? workUnitId;
    private Guid? productId;
    private Guid? shiftId;
    private ChartSeries[] trendSeries = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Bước 1 tải lookup/filter từ Application, lấy “Today” theo timezone nghiệp vụ do service trả.
            // Bước 2 tải Dashboard 7 ngày mặc định rồi khởi chạy một poll duy nhất cho page.
            filters = await Query.GetFilterOptionsAsync(lifetime.Token);
            toDate = filters.Today;
            fromDate = filters.Today.AddDays(-6);
            await LoadDashboardAsync();
            _ = PollAsync(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            error = "Không thể tải bộ lọc dashboard. " + exception.Message;
            loading = false;
        }
    }

    private IEnumerable<DashboardFilterOption> VisibleAreas =>
        // ParentId giúp lọc lookup trong bộ nhớ đã tải một lần, không tạo N+1 query khi người dùng đổi select.
        filters?.Areas.Where(option => !siteId.HasValue || option.ParentId == siteId) ?? [];

    private IEnumerable<DashboardFilterOption> VisibleWorkCenters
    {
        get
        {
            if (filters is null) return [];
            var allowedAreas = VisibleAreas.Select(option => option.Id).ToHashSet();
            return filters.WorkCenters.Where(option =>
                areaId.HasValue ? option.ParentId == areaId : allowedAreas.Contains(option.ParentId ?? Guid.Empty));
        }
    }

    private IEnumerable<DashboardFilterOption> VisibleWorkUnits
    {
        get
        {
            if (filters is null) return [];
            var allowedGroups = VisibleWorkCenters.Select(option => option.Id).ToHashSet();
            return filters.WorkUnits.Where(option =>
                workCenterId.HasValue ? option.ParentId == workCenterId : allowedGroups.Contains(option.ParentId ?? Guid.Empty));
        }
    }

    private IEnumerable<DashboardFilterOption> VisibleShifts =>
        filters?.Shifts.Where(option => !siteId.HasValue || option.ParentId == siteId) ?? [];

    private string SelectedScopeLabel => workUnitId.HasValue
        ? "Thiết bị: " + NameOf(filters?.WorkUnits, workUnitId)
        : workCenterId.HasValue
            ? "Dây chuyền: " + NameOf(filters?.WorkCenters, workCenterId)
            : siteId.HasValue ? "Nhà máy: " + NameOf(filters?.Sites, siteId) : "Toàn hệ thống";

    private async Task LoadDashboardAsync()
    {
        if (filters is null || fromDate > toDate) return;
        loading = true;
        error = null;
        lineOee = null;
        try
        {
            // DTO mang toàn bộ filter tới Application; EF Core aggregation nằm sau IDashboardQueryService.
            var request = new DashboardQueryRequest(fromDate, toDate, siteId, areaId, workCenterId, workUnitId, productId, shiftId);
            model = await Query.GetAsync(request, lifetime.Token);
            PrepareTrend(model);
            try
            {
                lineOee = await Query.GetLineOeeAsync(request, lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception)
            {
                lineOee = new(toDate, null, null, null, null, null, null,
                    "Không thể tải OEE Line. Hãy làm mới để thử lại.");
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            error = "Không thể tải dữ liệu dashboard. " + exception.Message;
        }
        finally
        {
            loading = false;
        }
    }

    private Task ReloadAsync() => LoadDashboardAsync();
    private async Task OpenLineOffAsync()
    {
        if (lineOee?.WorkCenterId is not { } lineId || lineOee.TimeZoneId is null) return;
        var parameters = new DialogParameters<Dashboard.Dialogs.LineOffPeriodsDialog>
        {
            { x => x.WorkCenterId, lineId },
            { x => x.Line, lineOee.Line ?? string.Empty },
            { x => x.TimeZoneId, lineOee.TimeZoneId },
            { x => x.Date, lineOee.Date }
        };
        var dialog = await DialogService.ShowAsync<Dashboard.Dialogs.LineOffPeriodsDialog>("Thời gian Off", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false });
        if (await dialog.Result is { Canceled: false, Data: true }) await LoadDashboardAsync();
    }
    private Task ApplyFiltersAsync()
    {
        activeRangeDays = 0;
        return fromDate <= toDate ? LoadDashboardAsync() : Task.CompletedTask;
    }

    private Task SetQuickRangeAsync(int days)
    {
        // Khoảng ngày dựa trên filters.Today của Site/query, không dùng ngày máy chủ làm mốc nghiệp vụ.
        if (filters is null) return Task.CompletedTask;
        activeRangeDays = days;
        toDate = filters.Today;
        fromDate = filters.Today.AddDays(-(days - 1));
        return LoadDashboardAsync();
    }

    private async Task ChangeFromDateAsync(ChangeEventArgs args)
    {
        if (DateOnly.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) fromDate = value;
        await ApplyFiltersAsync();
    }

    private async Task ChangeToDateAsync(ChangeEventArgs args)
    {
        if (DateOnly.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) toDate = value;
        await ApplyFiltersAsync();
    }

    private async Task OnSiteChangedAsync(ChangeEventArgs args)
    {
        // Khi đổi cha phải reset lựa chọn con để không gửi một tổ hợp scope không còn hợp lệ.
        siteId = ParseId(args.Value); areaId = null; workCenterId = null; workUnitId = null;
        if (shiftId.HasValue && !VisibleShifts.Any(option => option.Id == shiftId)) shiftId = null;
        await ApplyFiltersAsync();
    }
    private async Task OnAreaChangedAsync(ChangeEventArgs args) { areaId = ParseId(args.Value); workCenterId = null; workUnitId = null; await ApplyFiltersAsync(); }
    private async Task OnWorkCenterChangedAsync(ChangeEventArgs args) { workCenterId = ParseId(args.Value); workUnitId = null; await ApplyFiltersAsync(); }
    private async Task OnWorkUnitChangedAsync(ChangeEventArgs args) { workUnitId = ParseId(args.Value); await ApplyFiltersAsync(); }
    private async Task OnProductChangedAsync(ChangeEventArgs args) { productId = ParseId(args.Value); await ApplyFiltersAsync(); }
    private async Task OnShiftChangedAsync(ChangeEventArgs args) { shiftId = ParseId(args.Value); await ApplyFiltersAsync(); }

    private void PrepareTrend(DashboardModel dashboard)
    {
        // Contract lưu ratio 0..1; MudChart nhận điểm 0..100 để trục biểu diễn phần trăm.
        trendSeries =
        [
            new ChartSeries { Name = "OEE", Data = dashboard.Trend.Select(point => (double)((point.Oee ?? 0) * 100)).ToArray() },
            new ChartSeries { Name = "A", Data = dashboard.Trend.Select(point => (double)((point.Availability ?? 0) * 100)).ToArray() },
            new ChartSeries { Name = "P", Data = dashboard.Trend.Select(point => (double)((point.Performance ?? 0) * 100)).ToArray() },
            new ChartSeries { Name = "Q", Data = dashboard.Trend.Select(point => (double)((point.Quality ?? 0) * 100)).ToArray() },
        ];
    }

    private static DashboardRatioCardView[] RatioCards(DashboardModel dashboard) =>
    [
        // Nội dung mô tả công thức chỉ để học/hiển thị; giá trị đã được Application tính trước.
        new("Availability", "A · Sẵn sàng", dashboard.Availability, Icons.Material.Filled.PowerSettingsNew, "Thời gian vận hành / thời gian tải"),
        new("Performance", "P · Hiệu suất", dashboard.Performance, Icons.Material.Filled.Bolt, "Tốc độ thực tế so với tốc độ lý tưởng"),
        new("Quality", "Q · Chất lượng", dashboard.Quality, Icons.Material.Filled.Verified, "Sản phẩm đạt / tổng sản lượng"),
    ];

    private static DashboardProductionCardView[] ProductionCards(DashboardModel dashboard) =>
    [
        new("Tổng sản lượng", dashboard.Production.Total, Icons.Material.Filled.Inventory2, "total"),
        new("Sản phẩm đạt", dashboard.Production.Good, Icons.Material.Filled.CheckCircle, "good"),
        new("Sản phẩm lỗi", dashboard.Production.Reject, Icons.Material.Filled.Cancel, "reject"),
        new("Kế hoạch", dashboard.Production.Target, Icons.Material.Filled.Flag, "target"),
    ];

    private static Guid? ParseId(object? value) => Guid.TryParse(value?.ToString(), out var id) ? id : null;
    private static string IdValue(Guid? value) => value?.ToString() ?? string.Empty;
    private static string DateValue(DateOnly value) => value == default ? string.Empty : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string NameOf(IReadOnlyCollection<DashboardFilterOption>? options, Guid? id) => options?.FirstOrDefault(option => option.Id == id)?.Name ?? "—";
    private static string FormatRatio(decimal? value) => value.HasValue ? value.Value.ToString("P1", CultureInfo.CurrentCulture) : "—";
    private static string Delta(decimal? value) => !value.HasValue ? "không có kỳ trước" : $"{(value >= 0 ? "+" : string.Empty)}{value.Value.ToString("P1", CultureInfo.CurrentCulture)} so kỳ trước";
    private static Color MetricColor(string metric) => metric switch { "Availability" => Color.Info, "Performance" => Color.Warning, _ => Color.Success };
    private static Color ProductionColor(string kind) => kind switch { "good" => Color.Success, "reject" => Color.Error, "target" => Color.Warning, _ => Color.Info };
    private async Task PollAsync(CancellationToken cancellationToken)
    {
        // Một PeriodicTimer cho toàn page; callback quay về renderer qua InvokeAsync, không tạo timer cho từng card.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(UiOptions.Value.RefreshSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(LoadDashboardAsync);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        // Rời Dashboard sẽ hủy cả query đang chạy lẫn vòng auto-refresh.
        lifetime.Cancel();
        lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
