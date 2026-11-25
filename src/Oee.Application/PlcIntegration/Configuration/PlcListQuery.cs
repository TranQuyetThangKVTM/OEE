namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Query phân trang chung cho màn hình cấu hình/health PLC.</summary>
public sealed record PlcListQuery(
    string? Search = null,
    string? State = null,
    Guid? ScopeId = null,
    string? SortBy = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 25);
