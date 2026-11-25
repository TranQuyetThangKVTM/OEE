using Oee.Domain.Common;

namespace Oee.Domain.Entities.Production;

/// <summary>Khung thực hiện là [start, end); chỉ kết luận đạt/hụt khi kế hoạch kết thúc.</summary>
public static class ProductionPlanExecutionRules
{
    public static ProductionPlanExecutionStatusEnum GetStatus(DateTimeOffset start, DateTimeOffset end,
        decimal plannedQuantity, decimal actualQuantity, bool hasOutput, DateTimeOffset asOf)
    {
        if (asOf < start) return ProductionPlanExecutionStatusEnum.Pending;
        if (asOf < end) return ProductionPlanExecutionStatusEnum.InProgress;
        if (!hasOutput) return ProductionPlanExecutionStatusEnum.MissingOutput;
        return actualQuantity < plannedQuantity
            ? ProductionPlanExecutionStatusEnum.CompletedUnderTarget
            : ProductionPlanExecutionStatusEnum.CompletedOnTarget;
    }
}
