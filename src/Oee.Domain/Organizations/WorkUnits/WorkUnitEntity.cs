using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Thiết bị sản xuất thuộc một WorkCenter.</summary>
public sealed class WorkUnitEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public Guid WorkCenterId { get; private set; }
    public WorkCenterEntity WorkCenter { get; private set; } = null!;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public OperationalStateEnum OperationalState { get; private set; }
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private WorkUnitEntity()
    {
    }

    public static WorkUnitEntity Create(Guid workCenterId, string code, string name, string? description = null, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        return new WorkUnitEntity
        {
            WorkCenterId = DomainGuard.RequiredId(workCenterId),
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

    public void ObserveOperationalState(OperationalStateEnum state) => OperationalState = DomainGuard.Defined(state);
}
