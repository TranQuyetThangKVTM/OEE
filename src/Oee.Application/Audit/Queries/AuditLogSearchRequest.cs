namespace Oee.Application.Contracts.Audit;

/// <summary>Điều kiện server filter/sort/page audit; From/Until được so theo UTC.</summary>
public sealed record AuditLogSearchRequest(
    string? User = null,
    string? EntityType = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? Until = null,
    string? SortBy = null,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25);
