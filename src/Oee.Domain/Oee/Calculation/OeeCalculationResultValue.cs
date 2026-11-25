namespace Oee.Domain.Oee;

/// <summary>Kết quả OEE/reliability cùng cờ bất thường và hash phục vụ recalculation idempotent.</summary>
/// <remarks>Các tỷ lệ Availability, Performance, Quality và OEE dùng thang 0..1; null nghĩa là mẫu số bằng 0.</remarks>
public sealed record OeeCalculationResultValue(
    decimal CalendarMinutes,
    decimal LoadingMinutes,
    decimal AvailabilityLossMinutes,
    decimal UtilizationMinutes,
    decimal GoodQuantity,
    decimal? Availability,
    decimal? Performance,
    decimal? Quality,
    decimal? Oee,
    decimal? MtbfMinutes,
    decimal? MttrMinutes,
    IReadOnlyCollection<string> AnomalyFlags,
    string InputHash);
