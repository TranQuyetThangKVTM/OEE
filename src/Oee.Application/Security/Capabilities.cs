namespace Oee.Application.Security;

/// <summary>
/// Danh mục capability (năng lực) dùng thống nhất giữa policy server và việc ẩn/hiện thao tác trên UI.
/// </summary>
/// <remarks>
/// Capability mô tả hành động cụ thể hơn Role. UI có thể dùng nó để cải thiện trải nghiệm,
/// nhưng Application/API vẫn phải kiểm tra quyền ở server vì việc ẩn nút không phải hàng rào bảo mật.
/// </remarks>
public static class Capabilities
{
    public const string DashboardRead = "dashboard.read";
    public const string ReportsRead = "reports.read";
    public const string MasterDataRead = "master.read";
    public const string MasterDataWrite = "master.write";
    public const string ProductionPlanManage = "production.plan.manage";
    public const string DowntimeWrite = "downtime.write";
    public const string AlertsManage = "alerts.manage";
    public const string TargetsManage = "targets.manage";
    public const string SnapshotsManage = "snapshots.manage";
    public const string PlcManage = "plc.manage";
    public const string UsersManage = "users.manage";
    public const string AuditRead = "audit.read";
    public const string AuditRawJsonRead = "audit.raw-json.read";
    public const string AuditRetentionManage = "audit.retention.manage";

    /// <summary>Các quyền xem hiện chỉ yêu cầu đăng nhập; quyền thay đổi dữ liệu vẫn theo ma trận role.</summary>
    public static bool IsReadOnly(string capability)
        => capability is
        DashboardRead or ReportsRead or MasterDataRead or AuditRead or AuditRawJsonRead;

    /// <summary>Tập capability hợp lệ, đồng thời là toàn bộ quyền cấp cho Admin.</summary>
    public static IReadOnlySet<string> All
    {
        get;
    } = new HashSet<string>(StringComparer.Ordinal)
    {
        DashboardRead,
        ReportsRead,
        MasterDataRead,
        MasterDataWrite,
        ProductionPlanManage,
        DowntimeWrite,
        AlertsManage,
        TargetsManage,
        SnapshotsManage,
        PlcManage,
        UsersManage,
        AuditRead,
        AuditRawJsonRead,
        AuditRetentionManage,
    };
}
