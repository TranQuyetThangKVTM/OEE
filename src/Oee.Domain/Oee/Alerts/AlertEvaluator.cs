using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;

namespace Oee.Domain.Oee;

/// <summary>Domain service thuần xác định các metric OEE không đạt target.</summary>
public static class AlertEvaluator
{
    /// <summary>So sánh OEE/A/P/Q có giá trị với các ngưỡng tương ứng.</summary>
    /// <param name="result">Kết quả calculator trên thang ratio 0..1.</param>
    /// <param name="target">Target đã được selector chọn theo scope và ngày hiệu lực.</param>
    /// <returns>Mỗi metric dưới target tạo một evaluation; thấp hơn 90% target được xếp Critical.</returns>
    public static IReadOnlyCollection<AlertEvaluationValue> Evaluate(OeeCalculationResultValue result, OeeTargetEntity target)
    {
        // Tạo một tập đồng nhất để cùng một rule so sánh được áp dụng cho cả bốn metric.
        var values = new (AlertMetricEnum Metric, decimal? Actual, decimal Target)[]
        {
            (AlertMetricEnum.Oee, result.Oee, target.MinimumOee),
            (AlertMetricEnum.Availability, result.Availability, target.MinimumAvailability),
            (AlertMetricEnum.Performance, result.Performance, target.MinimumPerformance),
            (AlertMetricEnum.Quality, result.Quality, target.MinimumQuality),
        };
        return values.Where(value => value.Actual.HasValue && value.Actual < value.Target)
            .Select(value => new AlertEvaluationValue(value.Metric, value.Actual!.Value, value.Target,
                value.Actual < value.Target * 0.9m ? AlertSeverityEnum.Critical : AlertSeverityEnum.Warning)).ToArray();
    }
}
