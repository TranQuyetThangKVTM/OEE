using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Domain.Entities.Production;

/// <summary>Một dòng thiết bị, sản phẩm, ca và sản lượng trong mẫu kế hoạch.</summary>
public sealed class SampleProductionPlanItemEntity : SoftDeletableAuditedEntity
{
    public Guid SampleProductionPlanId { get; private set; }
    public SampleProductionPlanEntity SampleProductionPlan { get; private set; } = null!;
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public ProductEntity Product { get; private set; } = null!;
    public Guid ShiftId { get; private set; }
    public ShiftEntity Shift { get; private set; } = null!;
    public decimal PlannedQuantity { get; private set; }
    public string? Notes { get; private set; }

    private SampleProductionPlanItemEntity()
    {
    }

    public static SampleProductionPlanItemEntity Create(Guid sampleProductionPlanId, Guid workUnitId, Guid productId, Guid shiftId,
        decimal plannedQuantity, string? notes = null)
    {
        return new SampleProductionPlanItemEntity
        {
            SampleProductionPlanId = DomainGuard.RequiredId(sampleProductionPlanId),
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            ProductId = DomainGuard.RequiredId(productId),
            ShiftId = DomainGuard.RequiredId(shiftId),
            PlannedQuantity = DomainGuard.Positive(plannedQuantity),
            Notes = DomainGuard.Optional(notes, 500)
        };
    }

    public void Update(Guid workUnitId, Guid productId, Guid shiftId, decimal plannedQuantity, string? notes)
    {
        DomainGuard.RequiredId(workUnitId);
        DomainGuard.RequiredId(productId);
        DomainGuard.RequiredId(shiftId);
        DomainGuard.Positive(plannedQuantity);
        var normalizedNotes = DomainGuard.Optional(notes, 500);
        WorkUnitId = workUnitId;
        ProductId = productId;
        ShiftId = shiftId;
        PlannedQuantity = plannedQuantity;
        Notes = normalizedNotes;
    }
}
