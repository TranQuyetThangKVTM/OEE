namespace Oee.Application.Contracts.Modules;

public sealed record SamplePlanItemDetail(
    Guid Id,
    Guid WorkUnitId,
    string WorkUnit,
    Guid ProductId,
    string Product,
    Guid ShiftId,
    string Shift,
    decimal PlannedQuantity,
    string? Notes,
    string RowVersion,
    string? WorkUnitName = null,
    string? ProductName = null,
    string? ShiftName = null,
    TimeOnly? ShiftStart = null,
    TimeOnly? ShiftEnd = null);
