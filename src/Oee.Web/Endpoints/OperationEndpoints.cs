
namespace Oee.Web.Endpoints;
/// <summary>Map REST command endpoint cho kế hoạch, downtime, kế hoạch mẫu và cấu hình hiệu lực.</summary>
public static class OperationEndpoints
{
    /// <summary>Đăng ký các mutation có capability/antiforgery và ETag concurrency phù hợp.</summary>
    public static IEndpointRouteBuilder MapOperationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ProductionPlanEndpoints.Map(endpoints);
        DowntimeCommandEndpoints.Map(endpoints);
        SamplePlanEndpoints.Map(endpoints);
        EffectiveConfigurationEndpoints.Map(endpoints);
        return endpoints;
    }
}
