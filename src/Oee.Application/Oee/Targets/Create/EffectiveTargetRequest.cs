namespace Oee.Application.Contracts.Operations;

/// <summary>Request target versioned; bốn minimum ratio đều dùng thang 0..1.</summary>
public sealed record EffectiveTargetRequest(
    Guid? WorkUnitId,
    Guid? SiteId,
    OeePeriodTypeContract PeriodType,
    decimal MinimumOee,
    decimal MinimumAvailability,
    decimal MinimumPerformance,
    decimal MinimumQuality,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Note,
    string? PeriodCode = null);
