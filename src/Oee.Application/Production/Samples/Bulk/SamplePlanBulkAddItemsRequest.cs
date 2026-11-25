namespace Oee.Application.Contracts.Operations;

public sealed record SamplePlanBulkAddItemsRequest(
    IReadOnlyCollection<Guid> WorkUnitIds,
    IReadOnlyCollection<Guid> ShiftIds,
    Guid ProductId,
    decimal PlannedQuantity,
    string? Notes);
