namespace Oee.Domain.Common;

/// <summary>Entity có metadata về thời điểm và tác nhân tạo/cập nhật.</summary>
public abstract class AuditedEntity : Entity
{

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public void RecordCreation(DateTimeOffset at, string? actor = null)
    {
        var normalizedActor = DomainGuard.Optional(actor, 120);
        CreatedAt = at.ToUniversalTime();
        CreatedBy = normalizedActor;
    }

    public void RecordUpdate(DateTimeOffset at, string? actor = null)
    {
        var normalizedActor = DomainGuard.Optional(actor, 120);
        UpdatedAt = at.ToUniversalTime();
        UpdatedBy = normalizedActor ?? UpdatedBy;
    }
}
