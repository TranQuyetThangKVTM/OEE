using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;

namespace Oee.Domain.Oee;

/// <summary>Kết quả so sánh một metric thực tế với target, chưa được persist thành <see cref="OeeAlertEntity"/>.</summary>
public sealed record AlertEvaluationValue(AlertMetricEnum Metric, decimal Actual, decimal Target, AlertSeverityEnum Severity);
