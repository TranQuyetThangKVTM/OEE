using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oee.Application.Common;
using Oee.Application.PlcIntegration.WriteBack.Write;
using Oee.Infrastructure.BackgroundServices.Configuration;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>Ghi KPI OEE và trạng thái downtime về các output tag OPC UA theo chu kỳ cấu hình.</summary>
/// <remarks>Service chỉ được đăng ký khi WriteBackEnabled; mất kết nối/NoData sẽ bỏ qua để HMI giữ giá trị cuối.</remarks>
public sealed partial class OpcUaWriteBackWorker(IServiceScopeFactory scopeFactory, IOptions<OpcUaWorkerOptions> options, ILogger<OpcUaWriteBackWorker> logger) : BackgroundService
{
    /// <summary>Chạy một write-back tick theo PeriodicTimer cho đến khi Web shutdown.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.WriteBackSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken)) { try { await TickAsync(stoppingToken); } catch (Exception exception) { LogFailure(logger, exception); } }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new WriteOpcUaValuesCommand(), ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OPC UA write-back tick failed")] private static partial void LogFailure(ILogger logger, Exception exception);
}
