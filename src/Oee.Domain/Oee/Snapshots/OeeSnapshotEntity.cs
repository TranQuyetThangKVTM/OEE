using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Oee;

namespace Oee.Domain.Entities.Oee;

/// <summary>Kết quả OEE bất biến theo WorkUnit và một instance DAY/SHIFT đã tính.</summary>
/// <remarks>
/// Các tỷ lệ lưu 0..1. Các trường thời gian tổn thất lưu phút decimal. CalculationVersion + InputHash cho phép
/// Application nhận biết recalculation cùng dữ liệu và tránh tạo snapshot trùng.
/// </remarks>
public sealed class OeeSnapshotEntity : AuditedEntity
{
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public OeePeriodTypeEnum PeriodType { get; private set; }
    public string PeriodCode { get; private set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; private set; }
    public DateTimeOffset PeriodEnd { get; private set; }
    public decimal Availability { get; private set; }
    public decimal Performance { get; private set; }
    public decimal Quality { get; private set; }
    public decimal Oee { get; private set; }
    public decimal TotalMinutes { get; private set; }
    public decimal UnscheduledMinutes { get; private set; }
    public decimal LoadingMinutes { get; private set; }
    public decimal AvailabilityLossMinutes { get; private set; }
    public decimal UtilizationMinutes { get; private set; }
    public decimal PerformanceLossMinutes { get; private set; }
    public decimal NetUtilizationMinutes { get; private set; }
    public decimal QualityLossMinutes { get; private set; }
    public decimal ValueAddedMinutes { get; private set; }
    public decimal TotalQuantity { get; private set; }
    public decimal DefectQuantity { get; private set; }
    public decimal GoodQuantity { get; private set; }
    public string CalculationVersion { get; private set; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; private set; }
    public string InputHash { get; private set; } = string.Empty;
    public string? AnomalyFlags { get; private set; }

    private OeeSnapshotEntity()
    {
    }

    public static OeeSnapshotEntity Create(Guid workUnitId, OeePeriodTypeEnum periodType, string periodCode, DateTimeOffset periodStart,
        DateTimeOffset periodEnd, OeeCalculationResultValue result, decimal totalQuantity, decimal defectQuantity,
        DateTimeOffset calculatedAt, string calculationVersion)
    {
        var entity = new OeeSnapshotEntity
        {
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            PeriodType = DomainGuard.Defined(periodType),
            PeriodCode = DomainGuard.Required(periodCode, 40),
            PeriodStart = periodStart.ToUniversalTime(),
            CalculationVersion = DomainGuard.Required(calculationVersion, 40)
        };
        entity.ApplyCalculation(periodEnd, result, totalQuantity, defectQuantity, calculatedAt);
        return entity;
    }

    public void ApplyCalculation(DateTimeOffset periodEnd, OeeCalculationResultValue result, decimal totalQuantity, decimal defectQuantity, DateTimeOffset calculatedAt)
    {
        ArgumentNullException.ThrowIfNull(result);
        DomainGuard.Period(PeriodStart, periodEnd);
        DomainGuard.NonNegative(totalQuantity);
        DomainGuard.NonNegative(defectQuantity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(defectQuantity, totalQuantity);
        var availability = DomainGuard.Ratio(result.Availability ?? 0, nameof(result.Availability));
        var performance = DomainGuard.Ratio(result.Performance ?? 0, nameof(result.Performance));
        var quality = DomainGuard.Ratio(result.Quality ?? 0, nameof(result.Quality));
        var oee = DomainGuard.Ratio(result.Oee ?? 0, nameof(result.Oee));
        DomainGuard.NonNegative(result.CalendarMinutes, nameof(result.CalendarMinutes));
        DomainGuard.NonNegative(result.LoadingMinutes, nameof(result.LoadingMinutes));
        DomainGuard.NonNegative(result.AvailabilityLossMinutes, nameof(result.AvailabilityLossMinutes));
        DomainGuard.NonNegative(result.UtilizationMinutes, nameof(result.UtilizationMinutes));
        DomainGuard.NonNegative(result.GoodQuantity, nameof(result.GoodQuantity));
        if (result.LoadingMinutes > result.CalendarMinutes || result.UtilizationMinutes > result.LoadingMinutes
            || result.AvailabilityLossMinutes != result.LoadingMinutes - result.UtilizationMinutes || result.GoodQuantity != totalQuantity - defectQuantity)
            throw new ArgumentException("Calculated durations or quantities are inconsistent.", nameof(result));
        var hash = DomainGuard.Required(result.InputHash, 64, nameof(result.InputHash));
        var flags = System.Text.Json.JsonSerializer.Serialize(result.AnomalyFlags.Concat(new[] { result.Oee.HasValue ? null : "NotApplicable" }).Where(flag => flag is not null));
        DomainGuard.Optional(flags, 1000, nameof(result.AnomalyFlags));
        PeriodEnd = periodEnd.ToUniversalTime();
        CalculatedAt = calculatedAt.ToUniversalTime();
        InputHash = hash;
        AnomalyFlags = flags;
        Availability = availability;
        Performance = performance;
        Quality = quality;
        Oee = oee;
        TotalMinutes = result.CalendarMinutes;
        LoadingMinutes = result.LoadingMinutes;
        UnscheduledMinutes = TotalMinutes - LoadingMinutes;
        AvailabilityLossMinutes = result.AvailabilityLossMinutes;
        UtilizationMinutes = result.UtilizationMinutes;
        NetUtilizationMinutes = UtilizationMinutes * performance;
        PerformanceLossMinutes = UtilizationMinutes - NetUtilizationMinutes;
        ValueAddedMinutes = NetUtilizationMinutes * quality;
        QualityLossMinutes = NetUtilizationMinutes - ValueAddedMinutes;
        TotalQuantity = totalQuantity;
        DefectQuantity = defectQuantity;
        GoodQuantity = result.GoodQuantity;
    }
}
