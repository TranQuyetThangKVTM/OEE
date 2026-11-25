using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Doanh nghiệp ở cấp gốc của cây tổ chức, có thể được nhiều Site tham chiếu.</summary>
public sealed class EnterpriseEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private EnterpriseEntity()
    {
    }

    public static EnterpriseEntity Create(string code, string name, string? description = null, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        return new EnterpriseEntity
        {
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
