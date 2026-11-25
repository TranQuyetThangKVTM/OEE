using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Oee.Web.Endpoints;
using Oee.Web.Hubs;

namespace Oee.Web.Hosting;
internal static class WebEndpointRegistration
{
    public static void MapWebServiceEndpoints(WebApplication app)
    {
        app.MapStaticAssets();
        // /health phục vụ monitoring; /api/v1 được rate-limit và chỉ trả DTO/public contract.
        app.MapHealthChecks("/health", new HealthCheckOptions { AllowCachingResponses = false, });
        // IIS warm-up kiểm tra tiến trình đã khởi động; không chờ SQL/OPC sẵn sàng.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            AllowCachingResponses = false,
        }).AllowAnonymous();
        var api = app.MapGroup("/api/v1").WithGroupName("v1").RequireRateLimiting("api-read");
        api.MapUserEndpoints();
        api.MapAuditEndpoints();
        api.MapOperationEndpoints();
        api.MapMobileReadEndpoints();
        api.MapReportingEndpoints();
        // SignalR hub phát realtime DTO; OpenAPI chỉ công bố ở Development.
        app.MapHub<OeeLiveHub>("/hubs/oee-live");
        if (app.Environment.IsDevelopment())
            app.MapOpenApi("/openapi/{documentName}.json");
    }
}
