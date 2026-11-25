namespace Oee.Application.Contracts.Modules;

/// <summary>Aggregate mẫu dạng DTO gồm metadata cha và các dòng con.</summary>
public sealed record SamplePlanDetail(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int ItemCount,
    int WorkUnitCount,
    int ShiftCount,
    decimal TotalPlannedQuantity,
    string RowVersion);
