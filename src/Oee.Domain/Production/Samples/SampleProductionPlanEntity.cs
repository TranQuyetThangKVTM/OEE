using Oee.Domain.Common;

namespace Oee.Domain.Entities.Production;

/// <summary>Mẫu kế hoạch gồm thông tin chung và các dòng thiết bị, sản phẩm, ca.</summary>
public sealed class SampleProductionPlanEntity : SoftDeletableAuditedEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    private readonly List<SampleProductionPlanItemEntity> items = [];
    public IReadOnlyCollection<SampleProductionPlanItemEntity> Items => items.AsReadOnly();

    private SampleProductionPlanEntity()
    {
    }

    public static SampleProductionPlanEntity Create(string code, string name, string? description = null)
    {
        return new SampleProductionPlanEntity
        {
            Code = DomainGuard.Required(code, 32),
            Name = DomainGuard.Required(name, 200),
            Description = DomainGuard.Optional(description, 1000)
        };
    }

    public void Update(string code, string name, string? description)
    {
        var normalizedCode = DomainGuard.Required(code, 32);
        var normalizedName = DomainGuard.Required(name, 200);
        var normalizedDescription = DomainGuard.Optional(description, 1000);
        Code = normalizedCode;
        Name = normalizedName;
        Description = normalizedDescription;
    }
}
