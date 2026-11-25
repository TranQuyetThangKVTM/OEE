namespace Oee.Domain.Oee;

/// <summary>Toàn bộ input thuần cần cho một lần tính OEE; không chứa EF Entity hay clock hệ thống.</summary>
/// <remarks>Duration dùng phút decimal; quantity dùng decimal; IdealCycleMinutes có đơn vị phút/sản phẩm.</remarks>
public sealed record OeeCalculationInputValue(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal UnscheduledMinutes,
    decimal TotalQuantity,
    decimal DefectQuantity,
    decimal IdealCycleMinutes,
    IReadOnlyCollection<OeeDowntimeInputValue> Downtimes);
