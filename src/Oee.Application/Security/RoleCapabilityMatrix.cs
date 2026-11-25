namespace Oee.Application.Security;

/// <summary>
/// Ánh xạ bốn Role nghiệp vụ sang các capability mà từng Role được phép sử dụng.
/// </summary>
/// <remarks>
/// Đây là nguồn quy tắc tập trung để tránh rải chuỗi Role trong Razor và service. So sánh dùng
/// <see cref = "StringComparer.Ordinal"/> nên tên Role/capability phải đúng chính tả đã khai báo.
/// </remarks>
public static class RoleCapabilityMatrix
{
    public const string Admin = "Admin";
    public const string PlanningManager = "PlanningManager";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    // Ma trận chỉ mô tả quyền. Việc buộc policy vào endpoint/component được thực hiện ở Web.
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Matrix = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        [Admin] = Capabilities.All,
        [PlanningManager] = Set(
            Capabilities.DashboardRead,
            Capabilities.ReportsRead,
            Capabilities.MasterDataRead,
            Capabilities.MasterDataWrite,
            Capabilities.ProductionPlanManage,
            Capabilities.AlertsManage,
            Capabilities.TargetsManage,
            Capabilities.SnapshotsManage,
            Capabilities.PlcManage),
        [Operator] = Set(
            Capabilities.DashboardRead,
            Capabilities.ReportsRead,
            Capabilities.DowntimeWrite,
            Capabilities.AlertsManage),
        [Viewer] = Set(Capabilities.DashboardRead, Capabilities.ReportsRead, Capabilities.MasterDataRead),
    };
    /// <summary>Danh sách Role được hệ thống công nhận.</summary>
    public static IReadOnlyCollection<string> Roles => Matrix.Keys.ToArray();

    /// <summary>Kiểm tra một Role có capability yêu cầu hay không.</summary>
    public static bool RoleHasCapability(
        string role,
        string capability)
        => Matrix.TryGetValue(role, out var capabilities) && capabilities.Contains(capability);

    /// <summary>Kiểm tra ít nhất một Role của user có capability yêu cầu hay không.</summary>
    public static bool AnyRoleHasCapability(
        IEnumerable<string> roles,
        string capability)
        => roles.Any(role => RoleHasCapability(role, capability));

    private static HashSet<string> Set(params string[] capabilities)
        => new HashSet<string>(capabilities, StringComparer.Ordinal);
}
