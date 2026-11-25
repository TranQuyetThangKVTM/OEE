using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Dây chuyền hoặc trung tâm làm việc thuộc một Area.</summary>
public sealed class WorkCenterEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public Guid AreaId { get; private set; }
    public AreaEntity Area { get; private set; } = null!;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private WorkCenterEntity()
    {
    }

    public static WorkCenterEntity Create(Guid areaId, string code, string name, string? description = null, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        return new WorkCenterEntity
        {
            AreaId = DomainGuard.RequiredId(areaId),
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
