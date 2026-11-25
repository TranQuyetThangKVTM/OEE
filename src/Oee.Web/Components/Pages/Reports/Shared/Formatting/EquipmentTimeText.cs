using System.Globalization;
using Oee.Application.Contracts.Reporting;

namespace Oee.Web.Components.Pages.Reports;

internal static class EquipmentTimeText
{
    public static string Duration(decimal? minutes, bool compact = false)
    {
        if (!minutes.HasValue) return "—";
        var value = minutes.Value;
        if (value < 1) return (value * 60).ToString("N0", CultureInfo.CurrentCulture) + " giây";
        if (compact && value >= 60)
            return decimal.Floor(value / 60).ToString("N0", CultureInfo.CurrentCulture) + "h" +
                decimal.Floor(value % 60).ToString("00", CultureInfo.CurrentCulture);
        if (value >= 1440) return (value / 1440).ToString("N1", CultureInfo.CurrentCulture) + " ngày";
        if (value >= 60) return (value / 60).ToString("N1", CultureInfo.CurrentCulture) + " giờ";
        return value.ToString("N1", CultureInfo.CurrentCulture) + " phút";
    }
    public static string State(EquipmentTimelineState state) => state switch
    {
        EquipmentTimelineState.Running => "Chạy",
        EquipmentTimelineState.Failure => "Hỏng máy",
        EquipmentTimelineState.PlannedStop => "Dừng kế hoạch",
        EquipmentTimelineState.OtherStop => "Tổn thất khác",
        EquipmentTimelineState.Stopped => "Dừng máy",
        EquipmentTimelineState.NoProduction => "Không có kế hoạch",
        EquipmentTimelineState.Future => "Chưa tới",
        _ => "Chưa có dữ liệu"
    };
    public static string Local(DateTimeOffset value, string zone) =>
        TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(zone))
            .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
}
