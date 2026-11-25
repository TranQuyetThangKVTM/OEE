namespace Oee.Application.Contracts.Modules;

public sealed record SamplePlanListItem(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int ItemCount,
    int WorkUnitCount,
    int ShiftCount,
    decimal TotalPlannedQuantity,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    string? UpdatedBy = null);
