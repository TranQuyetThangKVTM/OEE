using System.Threading.RateLimiting;
using MudBlazor.Services;
using Oee.Application.Security;
using Oee.Infrastructure.PlcIntegration;
using Oee.Web.ErrorHandling;
using Oee.Web.Realtime;
using Oee.Web.Security;

namespace Oee.Web.Hosting;
internal static class WebPresentationRegistration
{
    public static void AddPresentationServices(WebApplicationBuilder builder)
    {
        // Boundary HTTP: ProblemDetails, OpenAPI Development, typed SignalR và rate limiting theo actor/IP.
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi("v1");
        builder.Services.AddSignalR();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("api-read", context => RateLimitPartition.GetSlidingWindowLimiter(context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous", _ => new SlidingWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
        });
        // Health check OPC UA và các provider MudBlazor/Razor Server cần thiết cho Presentation.
        builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
        builder.Services.AddHealthChecks().AddCheck<OpcUaRuntimeHealthCheck>("opc-ua");
        builder.Services.AddMudServices();
        builder.Services.AddScoped<Oee.Web.Chat.ChatRequestRunner>();
        builder.Services.AddScoped<ScopedPrincipalAccessor>();
        builder.Services.AddScoped<Oee.Web.Components.Pages.Production.ProductionPlanCreationRunner>();
        builder.Services.AddScoped<Oee.Web.Components.Pages.Plc.OpcTagMonitorRequests>();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddHttpContextAccessor();
        // Adapter theo request đưa current actor/capability vào Application mà không làm Application phụ thuộc HttpContext.
        builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
        builder.Services.AddScoped<ICapabilityChecker, HttpCapabilityChecker>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<RealtimeOeeState>();
    }
}
