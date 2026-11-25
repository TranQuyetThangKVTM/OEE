using Oee.Application.Contracts.Reporting;

namespace Oee.Application.Reporting;

/// <summary>Query dữ liệu dùng chung cho màn hình phân tích và bản PDF.</summary>
public interface IReportAnalysisQueryService
{
    /// <summary>Đọc lookup filter báo cáo.</summary>
    Task<ReportFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken);

    /// <summary>Chạy report query theo timezone/filter và trả DTO dùng chung.</summary>
    Task<ReportData> QueryAsync(ReportQuery query, CancellationToken cancellationToken);
}
