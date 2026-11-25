using Oee.Application.Contracts.Reporting;

namespace Oee.Application.Reporting;

/// <summary>Render dữ liệu báo cáo đã chuẩn hóa thành PDF.</summary>
/// <remarks>Bộ dựng PDF không tự tính lại OEE; nó dùng cùng <see cref = "ReportData"/> với UI.</remarks>
public interface IReportExportService
{
    /// <summary>Tạo nội dung PDF trong bộ nhớ để API trả file.</summary>
    byte[] RenderPdf(ReportData data);
}
