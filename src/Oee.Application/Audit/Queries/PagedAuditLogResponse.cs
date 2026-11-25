namespace Oee.Application.Contracts.Audit;

/// <summary>Trang audit cùng tổng số bản ghi sau filter.</summary>
public sealed record PagedAuditLogResponse(
    IReadOnlyCollection<AuditLogResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
