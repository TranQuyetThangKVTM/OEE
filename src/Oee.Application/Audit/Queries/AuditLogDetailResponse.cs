namespace Oee.Application.Contracts.Audit;

/// <summary>Chi tiết audit cho dialog; RawJson chỉ có khi server đã cho capability kỹ thuật.</summary>
public sealed record AuditLogDetailResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string? UserId,
    string? UserName,
    string Action,
    string ActionDisplayName,
    string EntityType,
    string EntityTypeDisplayName,
    string EntityId,
    string? EntitySubject,
    string? IpAddress,
    IReadOnlyCollection<AuditFieldChangeResponse> Fields,
    string? RawJson);
