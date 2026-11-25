namespace Oee.Domain.Common;

/// <summary>Tiến độ thực hiện được tính từ khung giờ và sản lượng, không lưu thay vòng đời kế hoạch.</summary>
public enum ProductionPlanExecutionStatusEnum
{
    Pending = 1,
    InProgress = 2,
    MissingOutput = 3,
    CompletedUnderTarget = 4,
    CompletedOnTarget = 5
}
