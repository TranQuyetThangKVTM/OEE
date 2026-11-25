namespace Oee.Application.Contracts.Operations;

/// <summary>Request bulk tạo tích Descartes WorkUnit × Shift hoặc sinh từ SamplePlan.</summary>
/// <remarks>Server luôn preview/validate lại và thực hiện transaction; UI không phải ranh giới chống duplicate.</remarks>
public sealed record BulkProductionPlanRequest(
    BulkPlanCreationModeContract Mode,
    IReadOnlyCollection<Guid> WorkUnitIds,
    IReadOnlyCollection<Guid> ShiftIds,
    Guid? ProductId,
    Guid? SamplePlanId,
    DateOnly AppliedDate,
    decimal? PlannedQuantity,
    string? Notes,
    IReadOnlyCollection<SamplePlanQuantityOverride>? QuantityOverrides = null);
