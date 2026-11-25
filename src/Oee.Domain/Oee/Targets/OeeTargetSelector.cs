using Oee.Domain.Entities.Oee;

namespace Oee.Domain.Oee;

/// <summary>Chọn target hiệu lực có độ ưu tiên cao nhất mà không đọc SQL hay clock.</summary>
public static class OeeTargetSelector
{
    /// <summary>Chọn theo WorkUnit → Site → global, sau đó period cụ thể và EffectiveFrom mới nhất.</summary>
    /// <param name="targets">Tập target ứng viên đã được tải bởi Application/Infrastructure.</param>
    /// <param name="workUnitId">Thiết bị đang tính.</param>
    /// <param name="siteId">Nhà máy của thiết bị.</param>
    /// <param name="date">Ngày local cần kiểm tra hiệu lực.</param>
    /// <param name="periodType">DAY hoặc SHIFT.</param>
    /// <param name="periodCode">Mã instance/kỳ cụ thể nếu có.</param>
    /// <returns>Target phù hợp nhất, hoặc null khi không có cấu hình.</returns>
    public static OeeTargetEntity? Select(IEnumerable<OeeTargetEntity> targets, Guid workUnitId, Guid siteId, DateOnly date,
        Common.OeePeriodTypeEnum periodType, string? periodCode = null) =>
        targets.Where(target => !target.IsDeleted
                && target.PeriodType == periodType
                && target.EffectiveFrom <= date
                && (!target.EffectiveTo.HasValue || target.EffectiveTo >= date)
                && (target.WorkUnitId == workUnitId || (target.WorkUnitId == null && (target.SiteId == siteId || target.SiteId == null)))
                && MatchesPeriod(target, periodType, periodCode))
            .OrderByDescending(target => target.WorkUnitId.HasValue)
            .ThenByDescending(target => target.SiteId.HasValue)
            .ThenByDescending(target => IsSpecificPeriod(target, periodType))
            .ThenByDescending(target => target.EffectiveFrom)
            .FirstOrDefault();

    private static bool MatchesPeriod(OeeTargetEntity target, Common.OeePeriodTypeEnum periodType, string? requestedCode)
    {
        if (string.IsNullOrWhiteSpace(requestedCode)) return true;
        var generic = periodType.ToString().ToUpperInvariant();
        return target.PeriodCode.Equals(generic, StringComparison.OrdinalIgnoreCase)
            || requestedCode.Equals(target.PeriodCode, StringComparison.OrdinalIgnoreCase)
            || requestedCode.Contains($"-{target.PeriodCode}-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecificPeriod(OeeTargetEntity target, Common.OeePeriodTypeEnum periodType) =>
        !target.PeriodCode.Equals(periodType.ToString(), StringComparison.OrdinalIgnoreCase);
}
