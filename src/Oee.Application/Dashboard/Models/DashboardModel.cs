namespace Oee.Application.Contracts.Modules;

/// <summary>View model hoàn chỉnh của dashboard; Razor chỉ hiển thị, không tự tính lại OEE.</summary>
public sealed record DashboardModel(
    DashboardRatioMetric Oee,
    DashboardRatioMetric Availability,
    DashboardRatioMetric Performance,
    DashboardRatioMetric Quality,
    DashboardProductionTotals Production,
    DashboardTimeTotals Times,
    IReadOnlyCollection<TrendPoint> Trend,
    IReadOnlyCollection<LossPoint> Losses,
    string TimeZoneId,
    bool HasData);
