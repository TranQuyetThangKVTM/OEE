using Oee.Domain.Common;

namespace Oee.Domain.Platform;

/// <summary>Bản ghi audit append-only chụp actor, action, subject, IP và JSON thay đổi của một mutation.</summary>
/// <remarks>
/// Audit không có navigation bắt buộc tới user/entity nhằm vẫn đọc được sau khi đối tượng đổi tên hoặc bị xóa.
/// Interceptor/Application service phải loại password, token và secret trước khi gán <see cref="Changes"/>.
/// </remarks>
public sealed class AuditLogEntity : Entity
{
    public DateTimeOffset Timestamp { get; private set; }
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? EntitySubject { get; private set; }
    public string? Changes { get; private set; }
    public string? IpAddress { get; private set; }

    private AuditLogEntity()
    {
    }

    public static AuditLogEntity Create(DateTimeOffset timestamp, string action, string entityType, string entityId,
        string? userId = null, string? userName = null, string? entitySubject = null, string? changes = null, string? ipAddress = null)
    {
        if (changes is not null)
        {
            using var document = System.Text.Json.JsonDocument.Parse(changes);
            if (document.RootElement.ValueKind is not (System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array))
                throw new ArgumentException("Audit changes must be a JSON object or array.", nameof(changes));
        }
        return new AuditLogEntity
        {
            Timestamp = timestamp.ToUniversalTime(),
            Action = DomainGuard.Required(action, 40),
            EntityType = DomainGuard.Required(entityType, 120),
            EntityId = DomainGuard.Required(entityId, 256),
            UserId = DomainGuard.Optional(userId, 64),
            UserName = DomainGuard.Optional(userName, 200),
            EntitySubject = DomainGuard.Optional(entitySubject, 300),
            Changes = changes,
            IpAddress = DomainGuard.Optional(ipAddress, 64)
        };
    }
}
