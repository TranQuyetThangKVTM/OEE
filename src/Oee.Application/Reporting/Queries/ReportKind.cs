namespace Oee.Application.Contracts.Reporting;

/// <summary>Các báo cáo PDF nghiệp vụ được hỗ trợ.</summary>
public enum ReportKind
{
    DeviceOee = 1,
    DailyOee,
    WeeklyKpi,
    MonthlyKpi,
    ShiftKpi,
    DowntimeEvents = 8,
    QualityLoss,
    Reliability,
}
