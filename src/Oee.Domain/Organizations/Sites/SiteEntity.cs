using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Nhà máy thuộc một Enterprise, lưu múi giờ dùng cho kế hoạch và ca.</summary>
public sealed class SiteEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public Guid EnterpriseId { get; private set; }
    public EnterpriseEntity Enterprise { get; private set; } = null!;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string TimeZoneId { get; private set; } = "SE Asia Standard Time";
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private SiteEntity()
    {
    }

    public static SiteEntity Create(Guid enterpriseId, string code, string name, string timeZoneId, string? description = null, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        return new SiteEntity
        {
            EnterpriseId = DomainGuard.RequiredId(enterpriseId),
            Code = DomainGuard.Required(code, 32),
            Name = DomainGuard.Required(name, 200),
            TimeZoneId = DomainGuard.Required(timeZoneId, 100),
            Description = DomainGuard.Optional(description, 500),
            Status = DomainGuard.Defined(status)
        };
    }

    public void Update(string code, string name, string timeZoneId, string? description)
    {
        string normalizedCode = DomainGuard.Required(code, 32);
        string normalizedName = DomainGuard.Required(name, 200);
        string normalizedZone = DomainGuard.Required(timeZoneId, 100);
        string? normalizedDescription = DomainGuard.Optional(description, 500);
        Code = normalizedCode;
        Name = normalizedName;
        TimeZoneId = normalizedZone;
        Description = normalizedDescription;
    }

    public void ChangeStatus(RecordStatusEnum status) => Status = DomainGuard.Defined(status);
}
