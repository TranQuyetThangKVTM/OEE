using Oee.Domain.Common;

namespace Oee.Domain.Platform;

/// <summary>Dấu vết quản trị của một lần dọn audit cũ, nằm ngoài bảng AuditLogs.</summary>
public sealed class AuditRetentionRunEntity : Entity
{
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public DateTimeOffset CutoffUtc { get; private set; }
    public int RetentionMonths { get; private set; }
    public int CandidateCount { get; private set; }
    public int DeletedCount { get; private set; }
    public string? ActorUserId { get; private set; }
    public string ActorUserName { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }

    private AuditRetentionRunEntity()
    {
    }

    public static AuditRetentionRunEntity Create(DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc, DateTimeOffset cutoffUtc,
        int retentionMonths, int candidateCount, int deletedCount, string actorUserName, string? actorUserId = null, string? ipAddress = null)
    {
        if (completedAtUtc < startedAtUtc) throw new ArgumentException("Completion cannot precede start.");
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionMonths, 1);
        if (candidateCount < 0 || deletedCount < 0) throw new ArgumentOutOfRangeException(nameof(deletedCount));
        return new AuditRetentionRunEntity
        {
            StartedAtUtc = startedAtUtc.ToUniversalTime(),
            CompletedAtUtc = completedAtUtc.ToUniversalTime(),
            CutoffUtc = cutoffUtc.ToUniversalTime(),
            RetentionMonths = retentionMonths,
            CandidateCount = candidateCount,
            DeletedCount = deletedCount,
            ActorUserName = DomainGuard.Required(actorUserName, 200),
            ActorUserId = DomainGuard.Optional(actorUserId, 64),
            IpAddress = DomainGuard.Optional(ipAddress, 64)
        };
    }
}
