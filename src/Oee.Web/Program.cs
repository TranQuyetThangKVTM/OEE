using System.Globalization;
using Oee.Web.Components;
using Oee.Web.Endpoints;
using Oee.Web.Hosting;
using Serilog;

// Composition root của Oee.Web: đây là nơi duy nhất ghép Blazor, API, Identity, EF Core, SignalR và OPC UA.
// File không chứa công thức OEE; các use case được gọi qua Application interface do Infrastructure hiện thực.
// Bootstrap logger giúp ghi lỗi xảy ra trước khi DI container và cấu hình Serilog đầy đủ được tạo.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture).CreateBootstrapLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    WebConfiguration.AddConnectionOptions(builder);
    // Structured logging đọc sink/level từ cấu hình và enrich thêm context của service/request.
    builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(services).Enrich.FromLogContext());
    WebConfiguration.AddUiOptions(builder);
    var identitySecurity = IdentityRegistration.AddIdentityOptions(builder);
    WebPresentationRegistration.AddPresentationServices(builder);
    InfrastructureRegistration.AddInfrastructureServices(builder);
    IdentityRegistration.AddIdentityCookies(builder, identitySecurity);
    var app = builder.Build();
    RequestPipeline.UseRequestPipeline(app);
    WebEndpointRegistration.MapWebServiceEndpoints(app);
    AccountEndpoints.MapAccountEndpoints(app);
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .RequireAuthorization();
    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Oee.Web terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
