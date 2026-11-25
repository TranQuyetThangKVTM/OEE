using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Domain.Entities.Oee;

/// <summary>Cảnh báo persisted khi một metric của snapshot thấp hơn target đã chọn.</summary>
/// <remarks>Đây là fact lịch sử; lifecycle Open/Acknowledged/Resolved do các method của entity bảo vệ.</remarks>
public sealed class OeeAlertEntity : AuditedEntity
{
    public Guid SnapshotId { get; private set; }
    public OeeSnapshotEntity Snapshot { get; private set; } = null!;
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public Guid? TargetId { get; private set; }
    public OeeTargetEntity? Target { get; private set; }
    public AlertMetricEnum Metric { get; private set; }
    public AlertSeverityEnum Severity { get; private set; }
    public AlertStateEnum State { get; private set; }
    public decimal ActualValue { get; private set; }
    public decimal TargetValue { get; private set; }
    public DateTimeOffset TriggeredAt { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? Note { get; private set; }

    private OeeAlertEntity()
    {
    }

    public static OeeAlertEntity Create(Guid snapshotId, Guid workUnitId, AlertMetricEnum metric, AlertSeverityEnum severity,
        decimal actualValue, decimal targetValue, DateTimeOffset triggeredAt, Guid? targetId = null)
    {
        return new OeeAlertEntity
        {
            SnapshotId = DomainGuard.RequiredId(snapshotId),
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            TargetId = DomainGuard.OptionalId(targetId),
            Metric = DomainGuard.Defined(metric),
            Severity = DomainGuard.Defined(severity),
            ActualValue = DomainGuard.Ratio(actualValue),
            TargetValue = DomainGuard.Ratio(targetValue),
            TriggeredAt = triggeredAt.ToUniversalTime(),
            State = AlertStateEnum.Open
        };
    }

    public void Refresh(decimal actualValue, decimal targetValue, AlertSeverityEnum severity)
    {
        if (State == AlertStateEnum.Resolved) throw new InvalidOperationException("A resolved alert cannot be re-evaluated.");
        DomainGuard.Ratio(actualValue);
        DomainGuard.Ratio(targetValue);
        DomainGuard.Defined(severity);
        ActualValue = actualValue;
        TargetValue = targetValue;
        Severity = severity;
    }

    public void Acknowledge(DateTimeOffset at, string actor, string? note)
    {
        if (State != AlertStateEnum.Open) throw new InvalidOperationException("Only an open alert can be acknowledged.");
        var normalizedActor = DomainGuard.Required(actor, 120);
        var normalizedNote = NormalizeNote(note, Note);
        State = AlertStateEnum.Acknowledged;
        AcknowledgedAt = at.ToUniversalTime();
        AcknowledgedBy = normalizedActor;
        Note = normalizedNote;
    }

    public void Resolve(DateTimeOffset at, string actor, string? note)
    {
        if (State == AlertStateEnum.Resolved) throw new InvalidOperationException("The alert has already been resolved.");
        var normalizedActor = DomainGuard.Required(actor, 120);
        var normalizedNote = NormalizeNote(note, $"Resolved by {normalizedActor}");
        State = AlertStateEnum.Resolved;
        ResolvedAt = at.ToUniversalTime();
        Note = normalizedNote;
    }

    private static string? NormalizeNote(string? note, string? fallback) =>
        DomainGuard.Optional(string.IsNullOrWhiteSpace(note) ? fallback : note, 500, nameof(note));
}
