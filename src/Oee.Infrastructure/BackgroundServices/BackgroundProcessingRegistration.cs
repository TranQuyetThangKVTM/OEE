using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.PlcIntegration;
using Oee.Infrastructure.BackgroundServices.Configuration;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>Đăng ký xử lý OEE/PLC trong Web, không cần một executable Worker riêng.</summary>
public static class BackgroundProcessingRegistration
{
    public static IServiceCollection AddOeeBackgroundProcessing(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<Oee.Infrastructure.Production.Automation.PlanAutomationStorageOptions>(
            configuration.GetSection(Oee.Infrastructure.Production.Automation.PlanAutomationStorageOptions.SectionName));
        var client = configuration.GetSection("OpcUa").Get<OpcUaClientOptions>();
        if (client is { Enabled: true })
        {
            configuration["OpcUaWorker:Adapter"] = "Sdk";
            configuration["OpcUaWorker:InitialReconnectSeconds"] =
                client.ReconnectDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        services.AddOptions<BackgroundProcessingOptions>()
            .Bind(configuration.GetSection(BackgroundProcessingOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OeeProcessingOptions>()
            .Bind(configuration.GetSection(OeeProcessingOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OpcUaWorkerOptions>()
            .Bind(configuration.GetSection(OpcUaWorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.ReadRefreshSeconds < options.NoDataSeconds,
                "OpcUaWorker.ReadRefreshSeconds must be less than NoDataSeconds.")
            .ValidateOnStart();

        var processing = configuration.GetSection(BackgroundProcessingOptions.SectionName)
            .Get<BackgroundProcessingOptions>() ?? new();
        if (!processing.Enabled || string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
            return services;

        services.AddHostedService<TechnicalHeartbeatService>();
        services.AddHostedService<AuditRetentionService>();
        services.AddHostedService<OeeSnapshotWorker>();
        services.AddHostedService<ProductionPlanScheduleWorker>();
        if (client is null || client.Enabled)
        {
            services.AddHostedService<OpcUaIngestionWorker>();
            var opc = configuration.GetSection(OpcUaWorkerOptions.SectionName).Get<OpcUaWorkerOptions>() ?? new();
            if (opc.WriteBackEnabled) services.AddHostedService<OpcUaWriteBackWorker>();
        }
        return services;
    }
}
