
namespace Oee.Web.Endpoints;
/// <summary>API xuất các báo cáo PDF và import fault mapping XLSX.</summary>
public static class ReportingEndpoints
{
    /// <summary>Map report export và XLSX template/preview/commit với quyền tương ứng.</summary>
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        PdfReportEndpoints.Map(endpoints);
        FaultMappingImportEndpoints.Map(endpoints);
        return endpoints;
    }
}
