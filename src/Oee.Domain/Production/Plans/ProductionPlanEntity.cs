using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Domain.Entities.Production;

/// <summary>Kế hoạch sản xuất versioned cho một WorkUnit × Product trong một khoảng thời gian.</summary>
/// <remarks>
/// Plan đã có output không sửa in-place mà tạo correction mới trong cùng VersionSeriesId. Hai source FK nullable
/// giữ traceability khi plan được sinh từ mẫu mà không sửa mẫu gốc.
/// </remarks>
public sealed class ProductionPlanEntity : SoftDeletableAuditedEntity
{
    public Guid VersionSeriesId { get; private set; }
    public Guid? ReplacesPlanId { get; private set; }
    public ProductionPlanEntity? ReplacesPlan { get; private set; }
    public int Version { get; private set; } = 1;
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public ProductEntity Product { get; private set; } = null!;
    public Guid? ShiftId { get; private set; }
    public ShiftEntity? Shift { get; private set; }
    public Guid? SourceSampleProductionPlanId { get; private set; }
    public SampleProductionPlanEntity? SourceSampleProductionPlan { get; private set; }
    public Guid? SourceSampleProductionPlanItemId { get; private set; }
    public SampleProductionPlanItemEntity? SourceSampleProductionPlanItem { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateTimeOffset PlannedStart { get; private set; }
    public DateTimeOffset PlannedEnd { get; private set; }
    public decimal PlannedQuantity { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>Thời điểm xóa khỏi kế hoạch sử dụng; bản ghi và sản lượng lịch sử vẫn được giữ.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    private ProductionPlanEntity()
    {
    }

    public static ProductionPlanEntity Create(Guid workUnitId, Guid productId, string code, DateTimeOffset plannedStart,
        DateTimeOffset plannedEnd, decimal plannedQuantity, Guid? shiftId = null, string? notes = null,
        Guid? sourceSampleProductionPlanId = null, Guid? sourceSampleProductionPlanItemId = null, Guid? id = null)
    {
        DomainGuard.Period(plannedStart, plannedEnd);
        var plan = new ProductionPlanEntity
        {
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            ProductId = DomainGuard.RequiredId(productId),
            Code = DomainGuard.Required(code, 32),
            PlannedStart = plannedStart.ToUniversalTime(),
            PlannedEnd = plannedEnd.ToUniversalTime(),
            PlannedQuantity = DomainGuard.Positive(plannedQuantity),
            ShiftId = DomainGuard.OptionalId(shiftId),
            Notes = DomainGuard.Optional(notes, 500),
            SourceSampleProductionPlanId = DomainGuard.OptionalId(sourceSampleProductionPlanId),
            SourceSampleProductionPlanItemId = DomainGuard.OptionalId(sourceSampleProductionPlanItemId)
        };
        if (id.HasValue) plan.Id = DomainGuard.RequiredId(id.Value, nameof(id));
        plan.VersionSeriesId = plan.Id;
        return plan;
    }

    public void Update(Guid workUnitId, Guid productId, string code, DateTimeOffset plannedStart,
        DateTimeOffset plannedEnd, decimal plannedQuantity, Guid? shiftId, string? notes)
    {
        if (DeletedAt.HasValue) throw new InvalidOperationException("A deleted plan cannot be edited.");
        DomainGuard.RequiredId(workUnitId);
        DomainGuard.RequiredId(productId);
        DomainGuard.OptionalId(shiftId);
        DomainGuard.Period(plannedStart, plannedEnd);
        DomainGuard.Positive(plannedQuantity);
        var normalizedCode = DomainGuard.Required(code, 32);
        var normalizedNotes = DomainGuard.Optional(notes, 500);
        WorkUnitId = workUnitId;
        ProductId = productId;
        Code = normalizedCode;
        ShiftId = shiftId;
        PlannedStart = plannedStart.ToUniversalTime();
        PlannedEnd = plannedEnd.ToUniversalTime();
        PlannedQuantity = plannedQuantity;
        Notes = normalizedNotes;
    }

    public ProductionPlanEntity CreateCorrection(DateTimeOffset at, string? code = null)
    {
        if (DeletedAt.HasValue) throw new InvalidOperationException("A deleted plan cannot be corrected.");
        var correction = Create(WorkUnitId, ProductId, code ?? $"{Code}-V{Version + 1}", PlannedStart, PlannedEnd,
            PlannedQuantity, ShiftId, Notes, SourceSampleProductionPlanId, SourceSampleProductionPlanItemId);
        correction.VersionSeriesId = VersionSeriesId == Guid.Empty ? Id : VersionSeriesId;
        correction.ReplacesPlanId = Id;
        correction.Version = checked(Version + 1);
        RecordUpdate(at);
        return correction;
    }

    public void Delete(DateTimeOffset at)
    {
        if (DeletedAt.HasValue) throw new InvalidOperationException("A deleted plan cannot be deleted again.");
        DeletedAt = at.ToUniversalTime();
    }
}
