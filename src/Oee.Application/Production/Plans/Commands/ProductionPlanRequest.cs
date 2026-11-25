namespace Oee.Application.Contracts.Operations;

/// <summary>Request tạo/cập nhật plan đơn; quantity là decimal và thời điểm là DateTimeOffset.</summary>
public sealed record ProductionPlanRequest(
    Guid WorkUnitId,
    Guid ProductId,
    Guid? ShiftId,
    string Code,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    decimal PlannedQuantity,
    string? Notes);
