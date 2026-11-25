using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Production;

namespace Oee.Domain.Entities.Production;

/// <summary>Fact sản lượng thực tế, bắt buộc gắn ProductionPlan và giữ WorkUnit/Product/Shift tại thời điểm ghi.</summary>
/// <remarks>
/// Automatic output không có active plan phải đi vào <see cref="ProductionReconciliationItemEntity"/> thay vì tạo entity
/// này. UnscheduledMinutes là duration decimal, không phải kiểu giờ trong ngày.
/// </remarks>
public sealed class ProductionOutputEntity : AuditedEntity
{
    public Guid ProductionPlanId { get; private set; }
    public ProductionPlanEntity ProductionPlan { get; private set; } = null!;
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public ProductEntity Product { get; private set; } = null!;
    public Guid? ShiftId { get; private set; }
    public ShiftEntity? Shift { get; private set; }
    public DateTimeOffset PeriodStart { get; private set; }
    public DateTimeOffset PeriodEnd { get; private set; }
    public decimal StandardSpeedPerMinute { get; private set; }
    public decimal TotalProducedQuantity { get; private set; }
    public decimal DefectQuantity { get; private set; }
    public decimal UnscheduledMinutes { get; private set; }
    public DataSourceEnum Source { get; private set; }
    public string? SourceNote { get; private set; }

    private ProductionOutputEntity()
    {
    }

    public static ProductionOutputEntity Create(Guid productionPlanId, Guid workUnitId, Guid productId, DateTimeOffset periodStart,
        DateTimeOffset periodEnd, decimal standardSpeedPerMinute, decimal totalProducedQuantity,
        DataSourceEnum source, decimal defectQuantity = 0, Guid? shiftId = null, decimal unscheduledMinutes = 0, string? sourceNote = null)
    {
        ProductionRules.ValidateOutput(totalProducedQuantity, defectQuantity, periodStart, periodEnd);
        return new ProductionOutputEntity
        {
            ProductionPlanId = DomainGuard.RequiredId(productionPlanId),
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            ProductId = DomainGuard.RequiredId(productId),
            ShiftId = DomainGuard.OptionalId(shiftId),
            PeriodStart = periodStart.ToUniversalTime(),
            PeriodEnd = periodEnd.ToUniversalTime(),
            StandardSpeedPerMinute = DomainGuard.Positive(standardSpeedPerMinute),
            TotalProducedQuantity = totalProducedQuantity,
            DefectQuantity = defectQuantity,
            UnscheduledMinutes = DomainGuard.NonNegative(unscheduledMinutes),
            Source = DomainGuard.Defined(source),
            SourceNote = DomainGuard.Optional(sourceNote, 500)
        };
    }
}
