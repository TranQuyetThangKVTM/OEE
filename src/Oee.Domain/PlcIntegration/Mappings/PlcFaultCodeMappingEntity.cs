using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Domain.PlcIntegration;

/// <summary>Ánh xạ fault code tới DowntimeReason, có thể global hoặc riêng WorkUnit.</summary>
/// <remarks>Application ưu tiên mapping riêng thiết bị trước global; cuối cùng mới dùng reason UNKNOWN.</remarks>
public sealed class PlcFaultCodeMappingEntity : SoftDeletableAuditedEntity
{
    public Guid? WorkUnitId { get; private set; }
    public WorkUnitEntity? WorkUnit { get; private set; }
    public string FaultCode { get; private set; } = string.Empty;
    public Guid DowntimeReasonId { get; private set; }
    public DowntimeReasonEntity DowntimeReason { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    private PlcFaultCodeMappingEntity()
    {
    }

    public static PlcFaultCodeMappingEntity Create(string faultCode, Guid downtimeReasonId, Guid? workUnitId = null, string? description = null, bool isActive = true)
    {
        return new PlcFaultCodeMappingEntity
        {
            FaultCode = DomainGuard.Required(faultCode, 64),
            DowntimeReasonId = DomainGuard.RequiredId(downtimeReasonId),
            WorkUnitId = DomainGuard.OptionalId(workUnitId),
            Description = DomainGuard.Optional(description, 500),
            IsActive = isActive
        };
    }

    public void Update(string faultCode, Guid downtimeReasonId, Guid? workUnitId, string? description, bool isActive)
    {
        var normalizedFault = DomainGuard.Required(faultCode, 64);
        var normalizedDescription = DomainGuard.Optional(description, 500);
        DomainGuard.RequiredId(downtimeReasonId);
        DomainGuard.OptionalId(workUnitId);
        FaultCode = normalizedFault;
        DowntimeReasonId = downtimeReasonId;
        WorkUnitId = workUnitId;
        Description = normalizedDescription;
        IsActive = isActive;
    }

    public void RestoreMapping(string faultCode, Guid downtimeReasonId, Guid? workUnitId, string? description, bool isActive)
    {
        Update(faultCode, downtimeReasonId, workUnitId, description, isActive);
        Restore();
    }

    public void Delete(DateTimeOffset at)
    {
        SoftDelete(at);
        IsActive = false;
    }
}
