using Oee.Domain.Entities.Production;

namespace Oee.Domain.Production;

/// <summary>Quy tắc sản lượng và điều kiện chỉnh sửa cần bằng chứng từ nhiều aggregate.</summary>
public static class ProductionRules
{
    public static void EnsureCanEdit(ProductionPlanEntity plan, bool hasOutput)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.DeletedAt.HasValue) throw new InvalidOperationException("A deleted plan cannot be edited.");
        if (hasOutput) throw new InvalidOperationException("A plan with production output must be corrected by creating a new version.");
    }

    public static void ValidateOutput(decimal total, decimal defects, DateTimeOffset start, DateTimeOffset end)
    {
        if (total < 0 || defects < 0 || defects > total) throw new ArgumentOutOfRangeException(nameof(defects));
        if (end <= start) throw new ArgumentException("Output period end must be after start.");
    }
}
