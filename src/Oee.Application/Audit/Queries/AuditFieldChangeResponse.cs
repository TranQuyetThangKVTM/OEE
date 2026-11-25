namespace Oee.Application.Contracts.Audit;

/// <summary>Một field audit đã sanitize và ánh xạ tên thân thiện, gồm giá trị Before/After.</summary>
public sealed record AuditFieldChangeResponse(
    string PropertyName,
    string DisplayName,
    string? Before,
    string? After);
