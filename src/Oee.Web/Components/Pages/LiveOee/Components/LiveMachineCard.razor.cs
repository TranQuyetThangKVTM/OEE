using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;

namespace Oee.Web.Components.Pages;

public partial class LiveMachineCard
{
    [Parameter, EditorRequired]
    public LiveMachineItem Machine { get; set; } = default!;

    private string StateText => Machine.ConnectivityState switch
    {
        "Disconnected" => "MẤT KẾT NỐI",
        "NoData" => "KHÔNG CÓ DỮ LIỆU",
        "BadTag" => "TAG LỖI",
        "NotConfigured" => "CHƯA CẤU HÌNH",
        _ when Machine.ConnectivityState != "Connected" => "CHƯA KẾT NỐI",
        _ when Machine.IsStale => "DỮ LIỆU CHƯA CẬP NHẬT",
        _ when Machine.OperationalState == "Running" => "ĐANG CHẠY",
        _ when Machine.OperationalState == "Stopped" => "ĐANG DỪNG",
        _ when Machine.OperationalState == "Setup" => "ĐANG CHUYỂN ĐỔI",
        _ when Machine.OperationalState == "Maintenance" => "ĐANG BẢO TRÌ",
        _ when Machine.OperationalState == "Cleaning" => "ĐANG VỆ SINH",
        _ when Machine.OperationalState == "Idle" => "ĐANG CHỜ",
        _ => "CHƯA XÁC ĐỊNH"
    };

    private string CardState => Machine.ConnectivityState == "NotConfigured" ? "unconfigured"
        : Machine.ConnectivityState != "Connected" || Machine.IsStale ? "offline"
        : Machine.OperationalState switch
        {
            "Running" => "running",
            "Stopped" => "stopped",
            "Setup" or "Maintenance" or "Cleaning" or "Idle" => "waiting",
            _ => "unknown"
        };

    private Color StateColor => CardState switch
    {
        "running" => Color.Success,
        "stopped" => Color.Error,
        "offline" => Color.Warning,
        "waiting" => Color.Info,
        _ => Color.Default
    };

    private string StateIcon => CardState switch
    {
        "running" => Icons.Material.Filled.PlayArrow,
        "stopped" => Icons.Material.Filled.Stop,
        "offline" => Icons.Material.Filled.WifiOff,
        "unconfigured" => Icons.Material.Filled.SettingsInputComponent,
        "waiting" => Icons.Material.Filled.Build,
        _ => Icons.Material.Filled.HelpOutline
    };

    private string ConnectivityText => Machine.ConnectivityState switch
    {
        "Connected" when Machine.IsStale => "PLC · Dữ liệu cũ",
        "Connected" => "PLC · Đã kết nối",
        "Disconnected" => "PLC · Mất kết nối",
        "NoData" => "PLC · Không có dữ liệu",
        "BadTag" => "PLC · Lỗi tag",
        "NotConfigured" => "PLC · Chưa cấu hình",
        _ => "PLC · Chưa xác định"
    };

    private Color ConnectivityColor => Machine.ConnectivityState switch
    {
        "Connected" when !Machine.IsStale => Color.Success,
        "BadTag" => Color.Error,
        "NotConfigured" => Color.Default,
        _ => Color.Warning
    };

    private static string Percent(decimal? value) => value?.ToString("P1", CultureInfo.CurrentCulture) ?? "—";
    private static string Number(decimal? value) => value?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";
    private static string Age(long? seconds) => seconds.HasValue ? seconds < 60 ? $"{seconds}s" : seconds < 3600 ? $"{seconds / 60}m{seconds % 60:00}s" : $"{seconds / 3600}h{seconds % 3600 / 60:00}m" : "chưa từng nhận";
}
