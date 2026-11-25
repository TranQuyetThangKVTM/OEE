using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oee.Application.Common;
using Oee.Application.Oee.Snapshots.RecalculateLookback;
using Oee.Infrastructure.BackgroundServices.Configuration;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>BackgroundService yêu cầu Application tính lại snapshot OEE theo chu kỳ cấu hình.</summary>
/// <remarks>Worker chỉ điều phối lịch chạy; công thức OEE nằm trong service/domain, không nằm trong vòng lặp này.</remarks>
public sealed partial class OeeSnapshotWorker(IServiceScopeFactory scopeFactory, IOptions<OeeProcessingOptions> options, ILogger<OeeSnapshotWorker> logger) : BackgroundService
{
    /// <summary>Tính ngay khi host khởi động rồi lặp theo chu kỳ, độc lập với mọi trang hoặc phiên đăng nhập.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SnapshotSeconds));
        do
        {
            // Cancellation khi shutdown là luồng bình thường; lỗi tick khác được log và tick sau vẫn tiếp tục.
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>();
                var command = new RecalculateRecentOeeSnapshotsCommand(
                    TimeSpan.FromHours(options.Value.SnapshotLookbackHours));
                await service.ExecuteAsync(command, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogFailure(logger, exception);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
    [LoggerMessage(Level = LogLevel.Error, Message = "OEE snapshot tick failed")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
