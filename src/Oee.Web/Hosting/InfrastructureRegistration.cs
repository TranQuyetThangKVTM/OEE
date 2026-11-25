using Oee.Infrastructure;
using Oee.Infrastructure.BackgroundServices;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Web.Realtime;

namespace Oee.Web.Hosting;
internal static class InfrastructureRegistration
{
    public static void AddInfrastructureServices(WebApplicationBuilder builder)
    {
        // Connection string được lấy từ configuration provider/secret, không được hard-code tại composition root.
        var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddOeeInfrastructure(defaultConnection);
        if (!string.IsNullOrWhiteSpace(defaultConnection))
            builder.Services.AddHostedService<RealtimeOeePublisher>();
        builder.Services.AddOeeBackgroundProcessing(builder.Configuration);
        var useFakeOpcUa = string.Equals(builder.Configuration["OpcUaWorker:Adapter"], "Fake", StringComparison.OrdinalIgnoreCase);
        builder.Services.AddOeeOpcUaIntegration(useFakeOpcUa);
    }
}
