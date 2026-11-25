namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng OEE target versioned; OEE/A/P/Q dùng thang 0..1.</summary>
public sealed record TargetListItem(
    Guid Id,
    Guid? WorkUnitId,
    Guid? SiteId,
    string Scope,
    string PeriodType,
    string PeriodCode,
    decimal Oee,
    decimal Availability,
    decimal Performance,
    decimal Quality,
    DateOnly From,
    DateOnly? To,
    string? Note,
    bool IsEffective,
    string RowVersion);
