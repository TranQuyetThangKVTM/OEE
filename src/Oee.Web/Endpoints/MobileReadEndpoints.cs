
namespace Oee.Web.Endpoints;
/// <summary>REST API v1 phục vụ mobile/hệ thống ngoài bằng DTO, không trả EF Entity.</summary>
public static class MobileReadEndpoints
{
    /// <summary>Map các endpoint production status/list, alert và master lookup.</summary>
    public static IEndpointRouteBuilder MapMobileReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ProductionReadEndpoints.Map(endpoints);
        DowntimeReadEndpoints.Map(endpoints);
        AlertApiEndpoints.Map(endpoints);
        MasterLookupEndpoints.Map(endpoints);
        return endpoints;
    }
}
