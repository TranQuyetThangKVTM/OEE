using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Domain.Entities.Production;

/// <summary>Lưu delta PLC chưa đủ bằng chứng gán kế hoạch/ca để không làm mất số liệu nguồn.</summary>
/// <remarks>Giữ tên bảng và các trường trạng thái lịch sử để tương thích SQL đã triển khai; không còn luồng đối soát thủ công.</remarks>
public sealed class ProductionReconciliationItemEntity : AuditedEntity
{
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public Guid? ProductId { get; private set; }
    public ProductEntity? Product { get; private set; }
    public string SourceKey { get; private set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; private set; }
    public decimal TotalDelta { get; private set; }
    public decimal DefectDelta { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public ReconciliationStateEnum State { get; private set; } = ReconciliationStateEnum.Pending;
    public Guid? ResolvedProductionPlanId { get; private set; }
    public ProductionPlanEntity? ResolvedProductionPlan { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }

    private ProductionReconciliationItemEntity()
    {
    }

    public static ProductionReconciliationItemEntity Create(Guid workUnitId, string sourceKey, DateTimeOffset capturedAt,
        decimal totalDelta, decimal defectDelta, string reasonCode, Guid? productId = null)
    {
        DomainGuard.NonNegative(totalDelta);
        DomainGuard.NonNegative(defectDelta);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(defectDelta, totalDelta);
        return new ProductionReconciliationItemEntity
        {
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            ProductId = DomainGuard.OptionalId(productId),
            SourceKey = DomainGuard.Required(sourceKey, 200),
            CapturedAt = capturedAt.ToUniversalTime(),
            TotalDelta = totalDelta,
            DefectDelta = defectDelta,
            ReasonCode = DomainGuard.Required(reasonCode, 64),
            State = ReconciliationStateEnum.Pending
        };
    }
}
