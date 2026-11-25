using Oee.Domain.Common;

namespace Oee.Domain.Oee;

/// <summary>Domain service thuần tính Availability, Performance, Quality, OEE, MTBF và MTTR.</summary>
/// <remarks>
/// Calculator không đọc SQL, không lấy thời gian hệ thống và không có side effect. Cùng input luôn cho cùng
/// result/hash, vì vậy UI, snapshot và PDF có thể dùng chung một công thức đã unit test.
/// </remarks>
public static class OeeCalculationEngine
{
    /// <summary>Phiên bản công thức được lưu cùng snapshot để truy vết rule đã sử dụng.</summary>
    public const string CalculationVersion = "OEE-v1";

    /// <summary>Tính các KPI OEE và reliability từ input đã tổng hợp.</summary>
    /// <param name="input">Kỳ, quantity, ideal cycle và downtime cần tính.</param>
    /// <returns>Kết quả theo phút/ratio, anomaly flags và SHA-256 input hash.</returns>
    /// <exception cref="ArgumentException">Khi kỳ kết thúc không sau kỳ bắt đầu.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Khi quantity/duration âm hoặc defect vượt total.</exception>
    public static OeeCalculationResultValue Calculate(OeeCalculationInputValue input)
    {
        // Bước 1: chặn input vô nghĩa trước khi thực hiện phép chia hoặc tổng hợp downtime.
        if (input.PeriodEnd <= input.PeriodStart) throw new ArgumentException("Period end must be after period start.", nameof(input));
        if (input.TotalQuantity < 0 || input.DefectQuantity < 0 || input.DefectQuantity > input.TotalQuantity)
            throw new ArgumentOutOfRangeException(nameof(input), "Defect quantity must be between zero and total quantity.");
        if (input.UnscheduledMinutes < 0 || input.IdealCycleMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Durations cannot be negative.");
        // Bước 2: Calendar và Loading dùng phút. Unscheduled vượt kỳ bị clamp và được gắn cờ.
        var anomalies = new List<string>();
        var calendar = Minutes(input.PeriodEnd - input.PeriodStart);
        var unscheduled = Math.Min(input.UnscheduledMinutes, calendar);
        if (input.UnscheduledMinutes > calendar) anomalies.Add("UnscheduledClamped");
        var loading = calendar - unscheduled;
        // Bước 3: cắt downtime theo biên kỳ, bỏ duplicate và tránh đếm hai lần phần overlap.
        var normalized = OeeDowntimeNormalizer.Normalize(input.Downtimes, input.PeriodStart, input.PeriodEnd, anomalies);
        var availabilityLoss = Math.Min(loading, normalized
            .Where(item => item.Category == DowntimeCategoryEnum.AvailabilityLoss)
            .Sum(item => Minutes(item.EndAt - item.StartAt)));
        var utilization = Math.Max(0, loading - availabilityLoss);
        var good = Math.Max(input.TotalQuantity - input.DefectQuantity, 0);
        // Bước 4: A = Utilization/Loading; P = Total×IdealCycle/Utilization; Q = Good/Total.
        // Divide trả null khi mẫu số 0; Clamp ghi cờ khi ratio vượt miền lưu trữ 0..1.
        var availability = Divide(utilization, loading);
        var performance = Divide(input.TotalQuantity * input.IdealCycleMinutes, utilization);
        var quality = Divide(good, input.TotalQuantity);
        availability = Clamp(availability, "AvailabilityClamped", anomalies);
        performance = Clamp(performance, "PerformanceClamped", anomalies);
        quality = Clamp(quality, "QualityClamped", anomalies);
        var oee = availability.HasValue && performance.HasValue && quality.HasValue
            ? availability * performance * quality : null;
        // Bước 5: Reliability chỉ tính Failure/Breakdown, không tính setup/CIP/planned maintenance.
        var failures = normalized.Where(item => item.Kind is DowntimeKindEnum.Failure or DowntimeKindEnum.Breakdown).ToArray();
        var failureMinutes = failures.Sum(item => Minutes(item.EndAt - item.StartAt));
        // Uptime loại toàn bộ khoảng dừng thực tế, độc lập với nhóm tổn thất OEE; không trừ Failure hai lần.
        var uptime = Math.Max(0, loading - normalized.Sum(item => Minutes(item.EndAt - item.StartAt)));
        decimal? mtbf = failures.Length == 0 ? null : uptime / failures.Length;
        decimal? mttr = failures.Length == 0 ? null : failureMinutes / failures.Length;
        // Bước 6: đóng gói result và hash canonical để nhận biết lần tính lại cùng input.
        return new OeeCalculationResultValue(calendar, loading, availabilityLoss, utilization, good,
            availability, performance, quality, oee, mtbf, mttr, anomalies, OeeInputHasher.Compute(input, normalized));
    }

    private static decimal Minutes(TimeSpan duration) => (decimal)duration.TotalMinutes;
    private static decimal? Divide(decimal numerator, decimal denominator) => denominator == 0 ? null : numerator / denominator;
    private static decimal? Clamp(decimal? value, string flag, List<string> anomalies)
    {
        if (!value.HasValue || value is >= 0 and <= 1) return value;
        anomalies.Add(flag);
        return Math.Clamp(value.Value, 0, 1);
    }
}
