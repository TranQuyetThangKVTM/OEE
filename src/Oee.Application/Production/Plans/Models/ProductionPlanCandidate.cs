using Oee.Domain.Entities.Organization;

namespace Oee.Application.Operations;

internal sealed record ProductionPlanCandidate(
    Guid WorkUnitId,
    string WorkUnit,
    Guid ShiftId,
    string Shift,
    Guid ProductId,
    string Product,
    decimal PlannedQuantity,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    Guid? SourceSamplePlanId,
    Guid? SourceSamplePlanItemId,
    string? Notes,
    string State,
    string? Reason)
{
    internal string WorkUnitName { get; init; } = string.Empty;
    internal string ShiftCode { get; init; } = string.Empty;
    internal string ProductCode { get; init; } = string.Empty;

    public static ProductionPlanCandidate Valid(
        WorkUnitEntity workUnit,
        ShiftEntity shift,
        ProductEntity product,
        decimal quantity,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? samplePlanId,
        Guid? sampleItemId,
        string? notes)
        => new(
            workUnit.Id,
            $"{workUnit.Code} · {workUnit.Name}",
            shift.Id,
            $"{shift.Code} · {shift.Name}",
            product.Id,
            $"{product.Code} · {product.Name}",
            quantity,
            start,
            end,
            samplePlanId,
            sampleItemId,
            notes,
            "Valid",
            null)
        {
            WorkUnitName = workUnit.Name,
            ShiftCode = shift.Code,
            ProductCode = product.Code
        };

    public static ProductionPlanCandidate Invalid(
        WorkUnitEntity workUnit,
        ShiftEntity shift,
        ProductEntity product,
        decimal quantity,
        string reason,
        Guid? samplePlanId = null,
        Guid? sampleItemId = null)
        => new(
            workUnit.Id,
            $"{workUnit.Code} · {workUnit.Name}",
            shift.Id,
            $"{shift.Code} · {shift.Name}",
            product.Id,
            $"{product.Code} · {product.Name}",
            quantity,
            default,
            default,
            samplePlanId,
            sampleItemId,
            null,
            "Skipped",
            reason);
}
