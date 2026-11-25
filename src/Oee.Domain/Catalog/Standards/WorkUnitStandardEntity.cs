using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>
/// Phiên bản định mức tốc độ của một tổ hợp <see cref="WorkUnit"/> × <see cref="Product"/> trong khoảng hiệu lực.
/// </summary>
/// <remarks>
/// Tốc độ có đơn vị sản phẩm/phút. Application ngăn khoảng hiệu lực chồng lấn; lịch sử được giữ bằng
/// EffectiveFrom/EffectiveTo thay vì ghi đè tốc độ cũ.
/// </remarks>
public sealed class WorkUnitStandardEntity : SoftDeletableAuditedEntity
{
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public ProductEntity Product { get; private set; } = null!;
    public decimal StandardSpeedPerMinute { get; private set; }
    public string Uom { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? ApprovedBy { get; private set; }

    private WorkUnitStandardEntity()
    {
    }

    public static WorkUnitStandardEntity Create(Guid workUnitId, Guid productId, decimal standardSpeedPerMinute, string uom,
        DateOnly effectiveFrom, DateOnly? effectiveTo = null, string? approvedBy = null)
    {
        DomainGuard.EffectiveRange(effectiveFrom, effectiveTo);
        return new WorkUnitStandardEntity
        {
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            ProductId = DomainGuard.RequiredId(productId),
            StandardSpeedPerMinute = DomainGuard.Positive(standardSpeedPerMinute),
            Uom = DomainGuard.Required(uom, 16),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            ApprovedBy = DomainGuard.Optional(approvedBy, 120)
        };
    }

    public void EndEffectiveness(DateOnly effectiveTo)
    {
        DomainGuard.EffectiveRange(EffectiveFrom, effectiveTo);
        EffectiveTo = effectiveTo;
    }
}
