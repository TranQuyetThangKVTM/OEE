namespace Oee.Domain.Common;

/// <summary>Nhóm tổn thất dùng phân loại downtime trong phân tích OEE.</summary>
public enum DowntimeCategoryEnum
{
    Unknown = 0,
    AvailabilityLoss = 1,
    PerformanceLoss = 2,
    QualityLoss = 3
}
