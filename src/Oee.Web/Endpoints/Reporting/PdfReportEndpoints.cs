using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;

namespace Oee.Web.Endpoints;
internal static class PdfReportEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/reports/automatic/{id:guid}/pdf", async (Guid id,
            Oee.Application.Common.IUseCaseExecutor requests, CancellationToken ct) =>
        {
            var report = await requests.ExecuteAsync(new Oee.Application.Production.Automation.GetAutomaticShiftPdfQuery(id), ct);
            return Results.File(report.Content, "application/pdf", report.FileName);
        }).RequireAuthorization();
        // PDF và UI cùng gọi IReportAnalysisQueryService nên không có công thức OEE thứ hai trong endpoint.
        endpoints.MapGet("/reports/{kind}/pdf", async (ReportKind kind, DateOnly fromDate, DateOnly toDate, Guid? siteId, Guid? areaId, Guid? workCenterId, Guid? workUnitId, Guid? processId, Guid? workUnitGroupId, Guid? productId, Guid? shiftId, Guid? downtimeReasonId, string? downtimeCategory, string? dataSource, string? search, IReportAnalysisQueryService queryService, IReportExportService exportService, CancellationToken ct) =>
        {
            if (!Enum.IsDefined(kind))
                return Results.NotFound();
            var data = await queryService.QueryAsync(new ReportQuery(kind, fromDate, toDate, siteId, areaId ?? processId, workCenterId ?? workUnitGroupId, workUnitId, productId, shiftId, downtimeReasonId, downtimeCategory, dataSource, search, Page: 1, PageSize: 5000), ct);
            return Results.File(exportService.RenderPdf(data), "application/pdf", $"{kind}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }).RequireAuthorization();
    }
}
