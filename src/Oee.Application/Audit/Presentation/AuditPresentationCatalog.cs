namespace Oee.Application.Contracts.Audit;

/// <summary>Registry tập trung ánh xạ tên Entity/action/property kỹ thuật sang nhãn tiếng Việt cho UI.</summary>
/// <remarks>Fallback giữ tên kỹ thuật để audit phiên bản cũ hoặc field mới vẫn mở được.</remarks>
public static class AuditPresentationCatalog
{
    public static IReadOnlyDictionary<string, string> EntityNames
    {
        get;
    } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ApplicationUser"] = "Tài khoản người dùng",
        ["ProductionPlan"] = "Kế hoạch sản xuất",
        ["PlcDeviceMapping"] = "Cấu hình PLC",
        ["Enterprise"] = "Doanh nghiệp",
        ["Site"] = "Nhà máy",
        ["Area"] = "Khu vực sản xuất",
        ["WorkCenter"] = "Dây chuyền / Work Center",
        // Audit cũ giữ nguyên tên kỹ thuật, chỉ ánh xạ nhãn hiển thị.
        ["Process"] = "Khu vực sản xuất",
        ["WorkUnitGroup"] = "Dây chuyền / Work Center",
        ["WorkUnit"] = "Thiết bị / Dây chuyền",
        ["Product"] = "Sản phẩm",
        ["Shift"] = "Ca làm việc",
        ["AuditLog"] = "Nhật ký thao tác",
    };
    public static IReadOnlyDictionary<string, string> ActionNames
    {
        get;
    } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Added"] = "Tạo mới",
        ["UserCreated"] = "Tạo mới",
        ["Modified"] = "Cập nhật",
        ["UserUpdated"] = "Cập nhật",
        ["UserRolesChanged"] = "Cập nhật vai trò",
        ["Deleted"] = "Xóa",
        ["UserDisabled"] = "Vô hiệu hóa",
        ["Activate"] = "Kích hoạt",
        ["Deactivate"] = "Tạm ngưng",
        ["UserPasswordReset"] = "Đặt lại mật khẩu",
        ["UserLocked"] = "Khóa",
        ["UserUnlocked"] = "Mở khóa",
        ["EndEffectiveness"] = "Kết thúc hiệu lực",
        ["RetentionCleanup"] = "Dọn log cũ",
    };
    public static IReadOnlyDictionary<string, string> FieldNames
    {
        get;
    } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Code"] = "Mã",
        ["Name"] = "Tên",
        ["DisplayName"] = "Tên hiển thị",
        ["Email"] = "Email",
        ["Roles"] = "Vai trò",
        ["IsDeleted"] = "Đã xóa",
        ["Status"] = "Trạng thái",
        ["State"] = "Trạng thái cảnh báo",
        ["PlannedStart"] = "Bắt đầu (kế hoạch)",
        ["PlannedEnd"] = "Kết thúc (kế hoạch)",
        ["PlannedQuantity"] = "Sản lượng kế hoạch",
        ["ProductId"] = "Sản phẩm",
        ["ShiftId"] = "Ca làm việc",
        ["WorkUnitId"] = "Thiết bị / Dây chuyền",
        ["WorkCenterId"] = "Dây chuyền / Work Center",
        ["AreaId"] = "Khu vực sản xuất",
        ["ProcessId"] = "Khu vực sản xuất",
        ["WorkUnitGroupId"] = "Dây chuyền / Work Center",
        ["Locked"] = "Trạng thái khóa",
        ["IsDisabled"] = "Vô hiệu hóa",
        ["PasswordChanged"] = "Mật khẩu đã thay đổi",
    };

    public static string EntityName(string name) => EntityNames.GetValueOrDefault(name, name);

    public static string ActionName(string name) => ActionNames.GetValueOrDefault(name, name);

    public static string FieldName(string name) => FieldNames.GetValueOrDefault(name, name);
}
