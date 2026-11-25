using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.Common;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class ProductionPlanValidator
{
    internal static void Validate(ProductionPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.PlannedEnd <= request.PlannedStart
            || request.PlannedQuantity <= 0)
        {
            throw new ArgumentException("Invalid production plan.");
        }
    }
}
