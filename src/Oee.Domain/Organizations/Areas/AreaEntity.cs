using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Khu vực sản xuất thuộc một Site.</summary>
public sealed class AreaEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public Guid SiteId { get; private set; }
    public SiteEntity Site { get; private set; } = null!;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private AreaEntity()
    {
    }

    public static AreaEntity Create(Guid siteId, string code, string name, string? description = null, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        return new AreaEntity
        {
            SiteId = DomainGuard.RequiredId(siteId),
            Code = DomainGuard.Required(code, 32),
            Name = DomainGuard.Required(name, 200),
            Description = DomainGuard.Optional(description, 500),
            Status = DomainGuard.Defined(status)
        };
    }

    public void Update(string code, string name, string? description)
    {
        string normalizedCode = DomainGuard.Required(code, 32);
        string normalizedName = DomainGuard.Required(name, 200);
        string? normalizedDescription = DomainGuard.Optional(description, 500);
        Code = normalizedCode;
        Name = normalizedName;
        Description = normalizedDescription;
    }

    public void ChangeStatus(RecordStatusEnum status) => Status = DomainGuard.Defined(status);
}
