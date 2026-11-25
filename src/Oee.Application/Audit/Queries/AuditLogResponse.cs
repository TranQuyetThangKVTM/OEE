namespace Oee.Application.Contracts.Audit;

/// <summary>DTO dòng audit cho bảng; cố ý không mang Changes/raw JSON để giảm lộ dữ liệu.</summary>
public sealed record AuditLogResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string EntityId,
    string? EntitySubject,
    string? IpAddress);
