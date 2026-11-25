using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Domain.Entities.Oee;

/// <summary>Ngưỡng OEE/A/P/Q có hiệu lực theo WorkUnit, Site hoặc global.</summary>
/// <remarks>Target là config versioned; selector ưu tiên scope cụ thể và khoảng hiệu lực phù hợp.</remarks>
public sealed class OeeTargetEntity : SoftDeletableAuditedEntity
{
    public Guid? WorkUnitId { get; private set; }
    public WorkUnitEntity? WorkUnit { get; private set; }
    public Guid? SiteId { get; private set; }
    public SiteEntity? Site { get; private set; }
    public OeePeriodTypeEnum PeriodType { get; private set; }
    public string PeriodCode { get; private set; } = string.Empty;
    public decimal MinimumOee { get; private set; }
    public decimal MinimumAvailability { get; private set; }
    public decimal MinimumPerformance { get; private set; }
    public decimal MinimumQuality { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? Note { get; private set; }

    private OeeTargetEntity()
    {
    }

    public static OeeTargetEntity Create(OeePeriodTypeEnum periodType, string periodCode, decimal minimumOee, decimal minimumAvailability,
        decimal minimumPerformance, decimal minimumQuality, DateOnly effectiveFrom, Guid? workUnitId = null,
        Guid? siteId = null, DateOnly? effectiveTo = null, string? note = null)
    {
        if (workUnitId.HasValue && siteId.HasValue) throw new ArgumentException("A target cannot belong to both WorkUnit and Site.");
        DomainGuard.EffectiveRange(effectiveFrom, effectiveTo);
        return new OeeTargetEntity
        {
            WorkUnitId = DomainGuard.OptionalId(workUnitId),
            SiteId = DomainGuard.OptionalId(siteId),
            PeriodType = periodType,
            PeriodCode = NormalizePeriodCode(periodType, periodCode),
            MinimumOee = DomainGuard.Ratio(minimumOee),
            MinimumAvailability = DomainGuard.Ratio(minimumAvailability),
            MinimumPerformance = DomainGuard.Ratio(minimumPerformance),
            MinimumQuality = DomainGuard.Ratio(minimumQuality),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Note = DomainGuard.Optional(note, 500)
        };
    }

    public void EndEffectiveness(DateOnly effectiveTo)
    {
        DomainGuard.EffectiveRange(EffectiveFrom, effectiveTo);
        EffectiveTo = effectiveTo;
    }

    public static string NormalizePeriodCode(OeePeriodTypeEnum periodType, string? value)
    {
        DomainGuard.Defined(periodType);
        var normalized = string.IsNullOrWhiteSpace(value) ? periodType.ToString().ToUpperInvariant() : value.Trim().ToUpperInvariant();
        if (normalized.Length > 40 || normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
            throw new ArgumentException("Period code must contain only letters, numbers, '-' or '_'.", nameof(value));
        return normalized;
    }
}
