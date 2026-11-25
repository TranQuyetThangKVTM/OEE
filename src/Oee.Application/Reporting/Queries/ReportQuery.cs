namespace Oee.Application.Contracts.Reporting;

/// <summary>Bộ lọc duy nhất dùng chung cho UI và PDF để không tính/report khác nhau.</summary>
public sealed record ReportQuery(
    ReportKind Kind,
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? SiteId = null,
    Guid? AreaId = null,
    Guid? WorkCenterId = null,
    Guid? WorkUnitId = null,
    Guid? ProductId = null,
    Guid? ShiftId = null,
    Guid? DowntimeReasonId = null,
    string? DowntimeCategory = null,
    string? DataSource = null,
    string? Search = null,
    string? SortBy = null,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25);
