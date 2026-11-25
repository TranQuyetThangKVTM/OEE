using Oee.Domain.Common;

namespace Oee.Domain.Platform;

/// <summary>Thông điệp persisted phục vụ store-and-forward/replay sau khi transaction nghiệp vụ commit.</summary>
/// <remarks>
/// <see cref="IdempotencyKey"/> chống xử lý trùng; ProcessedAt null là chưa hoàn tất; expiry và AttemptCount hỗ trợ
/// retention/retry. Worker xử lý side effect, Domain chỉ mô tả trạng thái message.
/// </remarks>
public sealed class OutboxMessageEntity : Entity
{
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessageEntity()
    {
    }

    public static OutboxMessageEntity Create(string idempotencyKey, DateTimeOffset occurredAt, string type, string payload, DateTimeOffset expiresAt)
    {
        if (expiresAt <= occurredAt) throw new ArgumentException("Expiry must be after occurrence.", nameof(expiresAt));
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("Payload is required.", nameof(payload));
        return new OutboxMessageEntity
        {
            IdempotencyKey = DomainGuard.Required(idempotencyKey, 200),
            OccurredAt = occurredAt.ToUniversalTime(),
            Type = DomainGuard.Required(type, 500),
            Payload = payload,
            ExpiresAt = expiresAt.ToUniversalTime()
        };
    }

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at.ToUniversalTime();
        LastError = null;
    }
    public void RecordFailure(string error)
    {
        var nextAttempt = checked(AttemptCount + 1);
        var normalized = DomainGuard.Optional(error[..Math.Min(error.Length, 4000)], 4000, nameof(error));
        AttemptCount = nextAttempt;
        LastError = normalized;
    }
}
