using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oee.Application.Audit;
using Oee.Application.Audit.Retention.Run;
using Oee.Application.Common;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>BackgroundService dọn AuditLog quá hạn theo retention cấu hình và để lại dấu vết quản trị.</summary>
/// <remarks>
/// Audit nghiệp vụ là append-only đối với UI/API. Cleanup chỉ xảy ra qua maintenance policy này; mỗi lần xóa có
/// bản ghi tổng hợp mới thay vì cho phép xóa tùy ý từng audit entry.
/// </remarks>
public sealed partial class AuditRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditRetentionOptions> options,
    ILogger<AuditRetentionService> logger) : BackgroundService
{
    /// <summary>Chạy cleanup lúc khởi động rồi lặp theo RunEveryHours cho đến khi Web dừng.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            LogRetentionDisabled(logger);
            return;
        }

        await TryCleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.RunEveryHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TryCleanupAsync(stoppingToken);
        }
    }

    private async Task TryCleanupAsync(CancellationToken cancellationToken)
    {
        try { await RunCleanupAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { LogCleanupFailure(logger, exception); }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>();
        var result = await processor.ExecuteAsync(new ProcessAuditRetentionCommand(), cancellationToken);
        if (result.DeletedCount > 0) LogRecordsDeleted(logger, result.DeletedCount, result.Cutoff);
    }


    [LoggerMessage(Level = LogLevel.Information, Message = "Audit retention is disabled")]
    private static partial void LogRetentionDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {DeletedCount} audit records older than {Cutoff}")]
    private static partial void LogRecordsDeleted(ILogger logger, int deletedCount, DateTimeOffset cutoff);
    [LoggerMessage(Level = LogLevel.Error, Message = "Audit cleanup failed; Web remains available and will retry")]
    private static partial void LogCleanupFailure(ILogger logger, Exception exception);
}
