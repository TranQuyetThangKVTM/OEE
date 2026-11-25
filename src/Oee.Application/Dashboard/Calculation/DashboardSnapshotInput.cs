namespace Oee.Application.Modules;

/// <summary>Một dòng thời gian đầu vào dùng để cộng gộp Availability trên Dashboard.</summary>
/// <param name = "LoadingMinutes">Thời gian máy được lên lịch để sản xuất, đơn vị phút.</param>
/// <param name = "UtilizationMinutes">Thời gian máy thực sự chạy trong Loading time, đơn vị phút.</param>
public sealed record DashboardSnapshotInput(
    decimal LoadingMinutes,
    decimal UtilizationMinutes);
