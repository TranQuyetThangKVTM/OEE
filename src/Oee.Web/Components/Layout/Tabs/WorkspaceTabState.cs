namespace Oee.Web.Components.Layout;

/// <summary>Danh sách tab điều hướng của một layout/circuit; không giữ các page/timer chạy ẩn.</summary>
public sealed class WorkspaceTabState
{
    public const int MaximumTabs = 20;
    private readonly List<WorkspaceTab> tabs = [new("/", "Bảng điều khiển")];
    public IReadOnlyList<WorkspaceTab> Tabs => tabs;
    public string ActiveUrl { get; private set; } = "/";
    public event Action? Changed;

    public void Open(string url)
    {
        if (!url.StartsWith('/') || url.StartsWith("//", StringComparison.Ordinal)
            || url.StartsWith("/account/login", StringComparison.OrdinalIgnoreCase)) return;
        if (ActiveUrl == url && tabs.Any(tab => tab.Url == url)) return;
        ActiveUrl = url;
        if (!tabs.Any(tab => tab.Url == url))
        {
            if (tabs.Count >= MaximumTabs) tabs.RemoveAt(1);
            tabs.Add(new(url, TitleFor(url.Split('?', '#')[0])));
        }
        Changed?.Invoke();
    }

    public void SetTitle(string title, string? url = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        var target = url ?? ActiveUrl;
        if (!tabs.Any(tab => tab.Url == target))
        {
            if (!target.StartsWith('/') || target.StartsWith("//", StringComparison.Ordinal)
                || target.StartsWith("/account/login", StringComparison.OrdinalIgnoreCase)) return;
            ActiveUrl = target;
            if (tabs.Count >= MaximumTabs) tabs.RemoveAt(1);
            tabs.Add(new(target, title));
            Changed?.Invoke();
            return;
        }
        var index = tabs.FindIndex(tab => tab.Url == target);
        if (index < 0 || tabs[index].Title == title) return;
        tabs[index] = tabs[index] with { Title = title };
        Changed?.Invoke();
    }

    public string? Close(string url)
    {
        if (url == "/") return null;
        var index = tabs.FindIndex(tab => tab.Url == url);
        if (index < 0) return null;
        tabs.RemoveAt(index);
        var next = url == ActiveUrl ? tabs[Math.Min(index, tabs.Count - 1)].Url : null;
        if (next is not null) ActiveUrl = next;
        Changed?.Invoke();
        return next;
    }

    private static string TitleFor(string path) => path.TrimEnd('/') switch
    {
        "" => "Bảng điều khiển",
        "/live-oee" => "Live OEE",
        "/production/plans" => "Kế hoạch sản xuất",
        "/production/plan-templates" => "Kế hoạch sản xuất mẫu",
        "/downtime" => "Phân loại dừng máy",
        "/master/products" => "Sản phẩm / SKU",
        "/master/work-unit-standards" => "Định mức tốc độ",
        "/master/shifts" => "Ca làm việc",
        "/master/downtime-reasons" => "Lý do dừng máy",
        "/reports/shift-kpi" => "KPI theo ca",
        "/reports/daily-oee" => "KPI ngày",
        "/reports/weekly-kpi" => "KPI tuần",
        "/reports/monthly-kpi" => "KPI tháng",
        "/reports/downtime-events" => "Sự kiện downtime",
        "/reports/reliability" => "Độ tin cậy",
        "/alerts" => "Cảnh báo",
        "/alerts/targets" => "Ngưỡng mục tiêu",
        "/plc/connections" => "Kết nối OPC UA",
        "/plc/mappings" or "/integration/plc-mappings" => "Ánh xạ thiết bị",
        "/plc/tag-monitor" => "Giám sát tag OPC UA",
        "/plc/faults" => "Ánh xạ mã lỗi",
        "/equipment/hierarchy" => "Cây cơ cấu tổ chức",
        "/users" => "Quản lý người dùng",
        "/audit" => "Nhật ký thao tác",
        _ when path.StartsWith("/production/plan-templates/", StringComparison.Ordinal) => "Chi tiết kế hoạch mẫu",
        _ => "Trang đang mở"
    };
}

public sealed record WorkspaceTab(string Url, string Title);
