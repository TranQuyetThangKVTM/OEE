using Oee.Infrastructure.Reporting;
using Oee.Web.Configuration;

namespace Oee.Web.Hosting;
internal static class WebConfiguration
{
    public static void AddConnectionOptions(WebApplicationBuilder builder)
    {
        var settingsFile = Environment.GetEnvironmentVariable("OEE_SETTINGS_FILE");
        if (!string.IsNullOrWhiteSpace(settingsFile))
            builder.Configuration.AddJsonFile(settingsFile, optional: false).AddEnvironmentVariables();
        builder.Services.AddOptions<Oee.Application.PlcIntegration.OpcUaClientOptions>().Bind(builder.Configuration.GetSection("OpcUa")).ValidateDataAnnotations().Validate(options => options.IsValid(), "OpcUa configuration is invalid or its authentication mode is unsupported.").ValidateOnStart();
    }

    public static void AddUiOptions(WebApplicationBuilder builder)
    {
        // Mọi option quan trọng được bind theo section và ValidateOnStart để fail-fast nếu cấu hình sai.
        builder.Services.AddOptions<DashboardUiOptions>().Bind(builder.Configuration.GetSection(DashboardUiOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<ReportingOptions>().Bind(builder.Configuration.GetSection(ReportingOptions.SectionName));
        builder.Services.AddOptions<RealtimeUiOptions>().Bind(builder.Configuration.GetSection(RealtimeUiOptions.SectionName)).ValidateDataAnnotations().Validate(options => options.StaleSeconds >= options.PublishSeconds, "Realtime stale threshold must be greater than or equal to its publish interval.").ValidateOnStart();
        builder.Services.AddOptions<Oee.Application.Audit.AuditRetentionOptions>().Bind(builder.Configuration.GetSection(Oee.Application.Audit.AuditRetentionOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
    }
}
