using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oee.Infrastructure.BackgroundServices.Configuration;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>BackgroundService ghi heartbeat định kỳ để hệ thống giám sát biết Web host còn sống.</summary>
public sealed partial class TechnicalHeartbeatService(
    ILogger<TechnicalHeartbeatService> logger,
    IOptions<BackgroundProcessingOptions> options) : BackgroundService
{
    // IOptions đã được ValidateOnStart trong Program nên interval luôn nằm trong miền hợp lệ.
    private readonly TimeSpan interval = TimeSpan.FromSeconds(options.Value.HeartbeatSeconds);

    /// <summary>Chạy cho đến khi host gửi stoppingToken; PeriodicTimer được dispose khi service dừng.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        LogWorkerStarted(logger);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            LogHeartbeat(logger, DateTimeOffset.UtcNow);
        }
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "OEE background processing started inside Web")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "OEE Web background processing heartbeat at {UtcTimestamp}")]
    private static partial void LogHeartbeat(ILogger logger, DateTimeOffset utcTimestamp);
}
