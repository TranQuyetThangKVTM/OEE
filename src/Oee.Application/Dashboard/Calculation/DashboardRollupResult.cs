namespace Oee.Application.Modules;

/// <summary>
/// Kết quả OEE đã cộng gộp theo tổng thời gian và tổng sản lượng, thay vì lấy trung bình các tỷ lệ con.
/// </summary>
/// <remarks>A/P/Q/OEE dùng thang 0..1; UI nhân 100 khi trình bày phần trăm.</remarks>
public sealed record DashboardRollupResult(
    decimal LoadingMinutes,
    decimal UtilizationMinutes,
    decimal IdealRunMinutes,
    decimal TotalQuantity,
    decimal GoodQuantity,
    decimal RejectQuantity,
    decimal? Availability,
    decimal? Performance,
    decimal? Quality,
    decimal? Oee);
