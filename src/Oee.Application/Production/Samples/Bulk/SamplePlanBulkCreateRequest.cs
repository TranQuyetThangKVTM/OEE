namespace Oee.Application.Contracts.Operations;

/// <summary>Tạo aggregate mẫu cha và tích Descartes WorkUnit × Shift trong một transaction.</summary>
public sealed record SamplePlanBulkCreateRequest(
    string Name,
    string? Description,
    IReadOnlyCollection<Guid> WorkUnitIds,
    IReadOnlyCollection<Guid> ShiftIds,
    Guid ProductId,
    decimal PlannedQuantity,
    string? Notes);
