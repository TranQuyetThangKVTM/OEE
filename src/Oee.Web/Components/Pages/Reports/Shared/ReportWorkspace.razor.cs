using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using MudBlazor;
using Oee.Application.Contracts.Reporting;
using Oee.Web.Components.Shared;
using ChartSeries = MudBlazor.ChartSeries<double>;

namespace Oee.Web.Components.Pages.Reports;

public partial class ReportWorkspace
{
    /// <summary>Loại dữ liệu/bố cục báo cáo cần truy vấn.</summary>
    [Parameter, EditorRequired] public ReportKind Kind { get; set; }
    /// <summary>Tiêu đề route báo cáo.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    /// <summary>Mô tả nguồn dữ liệu và ngữ cảnh báo cáo.</summary>
    [Parameter] public string Subtitle { get; set; } = "Dữ liệu OEE từ Application/EF Core, hiển thị theo timezone nhà máy";
    /// <summary>WorkUnit ban đầu khi route được mở từ một drill-down.</summary>
    [Parameter] public Guid? InitialWorkUnitId { get; set; }
    /// <summary>DowntimeReason ban đầu khi route được mở từ Pareto.</summary>
    [Parameter] public Guid? InitialReasonId { get; set; }
    /// <summary>Từ khóa ban đầu được chuyển từ màn hình nguồn.</summary>
    [Parameter] public string? InitialSearch { get; set; }

    // `filters` chứa lookup; `report` chứa kết quả tổng hợp/trend/breakdown; `table` điều khiển paging hiện tại.
    private ReportFilterOptions? filters; private ReportData? report; private PageTable<ReportTableRow>? table;
    private DateOnly fromDate; private DateOnly toDate; private Guid? siteId; private Guid? areaId; private Guid? groupId;
    private Guid? workUnitId; private Guid? productId; private Guid? shiftId; private Guid? reasonId;
    private string? search; private string? error;
    private bool filterLoading = true; private ChartSeries[] trendSeries = [];
    private ReportFilterOption? selectedUnit;
    private bool IsKpi => Kind is ReportKind.ShiftKpi or ReportKind.DailyOee or ReportKind.WeeklyKpi or ReportKind.MonthlyKpi;
    private bool IsCompactReport => IsKpi || Kind is ReportKind.Reliability or ReportKind.DowntimeEvents;
    private bool IsPeriodKpi => Kind is ReportKind.WeeklyKpi or ReportKind.MonthlyKpi;
    private bool CanGroupRows => Kind is ReportKind.ShiftKpi or ReportKind.DailyOee or ReportKind.DowntimeEvents or ReportKind.DeviceOee;
    private readonly TableGrouping<ReportTableRow> grouping = new(
        item => new((object?)item.WorkUnitId ?? item.WorkUnit ?? "unassigned", item.WorkUnit ?? "Chưa xác định máy"));
    private string? TableHeight => Kind == ReportKind.Reliability ? "560px" : !IsCompactReport ? null : IsPeriodKpi || Kind == ReportKind.DowntimeEvents ? "390px" : "max(320px, calc(100dvh - 410px))";
    private DateRange KpiDateRange => new(fromDate.ToDateTime(TimeOnly.MinValue), toDate.ToDateTime(TimeOnly.MinValue));

    protected override async Task OnInitializedAsync()
    {
        // Lookup và ngày nghiệp vụ Today đến từ Application để không phụ thuộc đồng hồ/ngày của Web server.
        try
        {
            filters = await Query.GetFilterOptionsAsync(CancellationToken.None);
            // Mặc định 30 ngày, sau đó áp dụng tham số drill-down và suy ra các cấp cha của WorkUnit.
            toDate = filters.Today;
            fromDate = filters.Today.AddDays(Kind switch
            {
                ReportKind.ShiftKpi or ReportKind.DailyOee or ReportKind.Reliability => -13,
                ReportKind.DowntimeEvents => 0,
                ReportKind.WeeklyKpi => -55,
                ReportKind.MonthlyKpi => -365,
                _ => -29
            });
            workUnitId = InitialWorkUnitId; reasonId = InitialReasonId; search = InitialSearch;
            SelectAncestors();
            selectedUnit = filters.WorkUnits.FirstOrDefault(x => x.Id == workUnitId);
            if (Kind == ReportKind.DowntimeEvents && filters.GeneratedAt.HasValue)
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(filters.Sites.FirstOrDefault()?.Detail ?? "Asia/Ho_Chi_Minh");
                var time = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(filters.GeneratedAt.Value, zone).DateTime);
                shiftId = VisibleShifts.FirstOrDefault(x => IsCurrentShift(x.Detail, time))?.Id;
            }
        }
        catch (Exception) { error = "Không thể tải bộ lọc báo cáo."; }
        finally { filterLoading = false; }
    }

    private async Task<TableData<ReportTableRow>> Load(TableState state, CancellationToken ct)
    {
        // PageTable chuyển sort/page; toàn bộ filter được đóng gói trong ReportQuery để EF Core xử lý ở SQL.
        try
        {
            error = null;
            var result = await Query.QueryAsync(new ReportQuery(Kind, fromDate, toDate, siteId, areaId,
                groupId, workUnitId, productId, shiftId, reasonId, Search: search,
                SortBy: state.SortLabel ?? (Kind == ReportKind.Reliability ? "events" : Kind == ReportKind.ShiftKpi ? "date" : IsKpi ? "code" : null),
                Descending: state.SortDirection == SortDirection.None ? !IsPeriodKpi : state.SortDirection == SortDirection.Descending,
                Page: state.Page + 1, PageSize: state.PageSize), ct);
            // Cùng ReportData nuôi metric, chart, Pareto và rows, tránh mỗi vùng tự chạy một công thức khác nhau.
            ct.ThrowIfCancellationRequested();
            report = result;
            if (!IsCompactReport) PrepareTrend(result);
            await InvokeAsync(StateHasChanged);
            return new TableData<ReportTableRow> { Items = result.Rows, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            error = "Không thể tải báo cáo."; report = null;
            await InvokeAsync(StateHasChanged); return new TableData<ReportTableRow> { Items = [], TotalItems = 0 };
        }
    }

    private Task ApplyFiltersAsync()
    {
        // Kiểm tra khoảng ngày trước khi yêu cầu bảng tải lại; backend tiếp tục validation request.
        if (fromDate > toDate) { error = "Ngày bắt đầu không được sau ngày kết thúc."; return Task.CompletedTask; }
        if (toDate.DayNumber - fromDate.DayNumber > 365) { error = "Khoảng báo cáo tối đa 366 ngày."; return Task.CompletedTask; }
        return table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;
    }

    private static bool IsCurrentShift(string? detail, TimeOnly now)
    {
        var parts = detail?.Split('–');
        if (parts?.Length != 2 || !TimeOnly.TryParse(parts[0], CultureInfo.InvariantCulture, out var start) ||
            !TimeOnly.TryParse(parts[1], CultureInfo.InvariantCulture, out var end)) return false;
        return end <= start ? now >= start || now < end : now >= start && now < end;
    }

    private Task ReloadAsync()
    {
        if (filters is null) return RetryFiltersAsync();
        if (fromDate > toDate) { error = "Ngày bắt đầu không được sau ngày kết thúc."; return Task.CompletedTask; }
        if (toDate.DayNumber - fromDate.DayNumber > 365) { error = "Khoảng báo cáo tối đa 366 ngày."; return Task.CompletedTask; }
        return table?.ReloadAsync() ?? Task.CompletedTask;
    }

    private async Task RetryFiltersAsync()
    {
        filterLoading = true;
        await OnInitializedAsync();
    }

    private async Task ChangeKpiRangeAsync(DateRange? range)
    {
        if (range?.Start is null || range.End is null)
        {
            error = "Vui lòng chọn đủ ngày bắt đầu và ngày kết thúc.";
            return;
        }
        fromDate = DateOnly.FromDateTime(range.Start.Value);
        toDate = DateOnly.FromDateTime(range.End.Value);
        await ApplyFiltersAsync();
    }

    private Task<IEnumerable<ReportFilterOption>> SearchUnitsAsync(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IEnumerable<ReportFilterOption> units = filters?.WorkUnits ?? [];
        if (!string.IsNullOrWhiteSpace(text))
            units = units.Where(x => x.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(units);
    }
    private static string UnitText(ReportFilterOption? unit) => unit is null ? string.Empty : unit.Code + " · " + unit.Name;
    private async Task ChangeUnitAsync(ReportFilterOption? unit)
    {
        selectedUnit = unit; workUnitId = unit?.Id;
        siteId = null; areaId = null; groupId = null;
        SelectAncestors();
        if (shiftId.HasValue && !VisibleShifts.Any(x => x.Id == shiftId)) shiftId = null;
        await ApplyFiltersAsync();
    }

    private string KpiDate(ReportTableRow row)
    {
        var code = row.Code;
        if (Kind == ReportKind.ShiftKpi && code?.Length >= 8 &&
            DateOnly.TryParseExact(code[^8..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var shiftDate))
            return shiftDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
        if (DateOnly.TryParseExact(code, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return day.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
        return row.Label;
    }

    private string KpiShift(ReportTableRow row) => row.Shift ?? filters?.Shifts.FirstOrDefault(x =>
        row.Code?.StartsWith("SHIFT-" + x.Code + "-", StringComparison.Ordinal) == true)?.Name ?? "—";
    private static Color OeeColor(decimal? value) => value is null ? Color.Default : value >= .85m ? Color.Success : value >= .65m ? Color.Warning : Color.Error;

    private static string PeriodRange(ReportTableRow row)
    {
        if (row.Code?.Length == 8 && row.Code[4..6] == "-W" &&
            int.TryParse(row.Code[..4], CultureInfo.InvariantCulture, out var year) &&
            int.TryParse(row.Code[6..], CultureInfo.InvariantCulture, out var week))
        {
            var start = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
            return start.ToString("dd/MM", CultureInfo.CurrentCulture) + " – " + start.AddDays(6).ToString("dd/MM", CultureInfo.CurrentCulture);
        }
        return string.Empty;
    }

    private Task SetRangeAsync(int days) { if (filters is null) return Task.CompletedTask; toDate = filters.Today; fromDate = filters.Today.AddDays(-(days - 1)); return ApplyFiltersAsync(); }

    // Các tập Visible duy trì chuỗi Site → Area → Group → WorkUnit ngay trên bộ lookup đã tải.
    private IEnumerable<ReportFilterOption> VisibleAreas => filters?.Areas.Where(x => !siteId.HasValue || x.ParentId == siteId) ?? [];
    private IEnumerable<ReportFilterOption> VisibleWorkCenters
    {
        get { var areaIds = VisibleAreas.Select(x => x.Id).ToHashSet(); return filters?.WorkCenters.Where(x => areaId.HasValue ? x.ParentId == areaId : areaIds.Contains(x.ParentId ?? Guid.Empty)) ?? []; }
    }
    private IEnumerable<ReportFilterOption> VisibleWorkUnits
    {
        get { var groupIds = VisibleWorkCenters.Select(x => x.Id).ToHashSet(); return filters?.WorkUnits.Where(x => groupId.HasValue ? x.ParentId == groupId : groupIds.Contains(x.ParentId ?? Guid.Empty)) ?? []; }
    }
    private IEnumerable<ReportFilterOption> VisibleShifts => filters?.Shifts.Where(x => !siteId.HasValue || x.ParentId == siteId) ?? [];

    private void SelectAncestors()
    {
        // Khi drill-down truyền WorkUnitId, tự chọn Group/Area/Site cha để các dropdown nhất quán.
        if (!workUnitId.HasValue || filters is null) return;
        var unit = filters.WorkUnits.FirstOrDefault(x => x.Id == workUnitId); groupId = unit?.ParentId;
        var group = filters.WorkCenters.FirstOrDefault(x => x.Id == groupId); areaId = group?.ParentId;
        var area = filters.Areas.FirstOrDefault(x => x.Id == areaId); siteId = area?.ParentId;
    }
    // Đổi cấp cha phải xóa lựa chọn con; nếu Shift không thuộc Site mới thì cũng bỏ Shift.
    private async Task OnSiteChangedAsync(Guid? value) { siteId = value; areaId = null; groupId = null; workUnitId = null; if (shiftId.HasValue && !VisibleShifts.Any(x => x.Id == shiftId)) shiftId = null; await ApplyFiltersAsync(); }
    private async Task OnAreaChangedAsync(Guid? value) { areaId = value; groupId = null; workUnitId = null; await ApplyFiltersAsync(); }
    private async Task OnWorkCenterChangedAsync(Guid? value) { groupId = value; workUnitId = null; await ApplyFiltersAsync(); }
    private async Task ChangeFromAsync(ChangeEventArgs e) { if (DateOnly.TryParse(e.Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) { fromDate = value; await ApplyFiltersAsync(); } }
    private async Task ChangeToAsync(ChangeEventArgs e) { if (DateOnly.TryParse(e.Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) { toDate = value; await ApplyFiltersAsync(); } }

    private void PrepareTrend(ReportData data)
    {
        // MudChart nhận double theo thang 0..100; DTO vẫn giữ ratio chính xác ở thang 0..1.
        trendSeries =
        [
            new ChartSeries { Name = "OEE", Data = data.Trend.Select(x => (double)((x.Oee ?? 0) * 100)).ToArray() },
            new ChartSeries { Name = "A", Data = data.Trend.Select(x => (double)((x.Availability ?? 0) * 100)).ToArray() },
            new ChartSeries { Name = "P", Data = data.Trend.Select(x => (double)((x.Performance ?? 0) * 100)).ToArray() },
            new ChartSeries { Name = "Q", Data = data.Trend.Select(x => (double)((x.Quality ?? 0) * 100)).ToArray() },
        ];
    }

    private string ExportUrl
    {
        get
        {
            // Dùng đúng bộ lọc đang hiển thị để endpoint PDF gọi lại cùng Application Query với UI.
            var values = new Dictionary<string, string?>
            {
                ["fromDate"] = DateValue(fromDate), ["toDate"] = DateValue(toDate),
                ["siteId"] = IdValue(siteId), ["areaId"] = IdValue(areaId), ["workCenterId"] = IdValue(groupId),
                ["workUnitId"] = IdValue(workUnitId), ["productId"] = IdValue(productId), ["shiftId"] = IdValue(shiftId),
                ["downtimeReasonId"] = IdValue(reasonId),
                ["search"] = search,
            };
            return QueryHelpers.AddQueryString(
                $"/api/v1/reports/{Kind}/pdf",
                values.Where(x => !string.IsNullOrWhiteSpace(x.Value)));
        }
    }

    private static string IdValue(Guid? value) => value?.ToString() ?? string.Empty;
    private static string DateValue(DateOnly value) => value == default ? string.Empty : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    // Ratio được lưu/truyền theo 0..1; format P1 chỉ đổi cách hiển thị sang 0..100%.
    private static string Percent(decimal? value) => value.HasValue ? value.Value.ToString("P1", CultureInfo.CurrentCulture) : "—";
    private string Local(DateTimeOffset? value)
    {
        // EndAt null biểu diễn downtime đang mở; timestamp có giá trị được đổi sang timezone của ReportData/Site.
        if (!value.HasValue) return "Đang mở";
        var zoneId = report?.TimeZoneId ?? "Asia/Ho_Chi_Minh";
        // Chỉ quy đổi lúc render, không thay đổi DateTimeOffset đã lưu trong SQL.
        try { return TimeZoneInfo.ConvertTime(value.Value, TimeZoneInfo.FindSystemTimeZoneById(zoneId)).ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture); }
        catch (TimeZoneNotFoundException) { return value.Value.ToUniversalTime().ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.CurrentCulture); }
    }
    private static string FormatMetric(ReportMetric metric) => !metric.Value.HasValue ? "—" : metric.IsRatio ? metric.Value.Value.ToString("P1", CultureInfo.CurrentCulture) : $"{metric.Value.Value:N1} {metric.Unit}".Trim();
    private static string MetricIcon(string key) => key switch { "oee" => Icons.Material.Filled.DonutLarge, "availability" => Icons.Material.Filled.PowerSettingsNew, "performance" => Icons.Material.Filled.Bolt, "quality" => Icons.Material.Filled.Verified, "defect" => Icons.Material.Filled.Cancel, "mtbf" or "mttr" => Icons.Material.Filled.BuildCircle, _ => Icons.Material.Filled.Analytics };
    private static Color MetricColor(string key) => key switch { "oee" => Color.Secondary, "quality" or "good" => Color.Success, "defect" or "unplanned" => Color.Error, "performance" or "planned" => Color.Warning, _ => Color.Primary };
}
