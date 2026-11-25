namespace Oee.Application.Contracts.Modules;

/// <summary>Sản lượng đạt theo thời gian nhà máy; tuần ISO bắt đầu thứ Hai, dữ liệu fact tính theo PeriodEnd.</summary>
public sealed record LiveProductionPeriods(
    string? ShiftCode,
    DateOnly LocalDate,
    int WeekNumber,
    decimal? ShiftQuantity,
    decimal DayQuantity,
    decimal WeekQuantity,
    decimal MonthQuantity,
    decimal? CurrentProductQuantity);
