using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Oee.Application.Contracts.Api;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Web.Configuration;
using Oee.Web.Hubs;

namespace Oee.Web.Realtime;

/// <summary>BackgroundService phía Web lấy Live OEE định kỳ và broadcast typed DTO qua SignalR.</summary>
/// <remarks>Chỉ tạo khi có database connection; mỗi tick mở scope mới để dùng query service scoped an toàn.</remarks>
public sealed partial class RealtimeOeePublisher(
    IServiceScopeFactory scopeFactory,
    RealtimeOeeState state,
    IHubContext<OeeLiveHub, IOeeLiveClient> hub,
    IOptions<RealtimeUiOptions> options,
    TimeProvider timeProvider,
    ILogger<RealtimeOeePublisher> logger) : BackgroundService
{
    /// <summary>Publish ngay khi startup rồi lặp theo PublishSeconds bằng TimeProvider có thể kiểm thử.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await PublishOnceAsync(stoppingToken);

            using PeriodicTimer timer = new(
                TimeSpan.FromSeconds(options.Value.PublishSeconds),
                timeProvider);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PublishOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                throw;
            }
        }
    }

    /// <summary>Lấy model mới, cập nhật cache và broadcast; khi lỗi giữ giá trị cuối và báo Reconnecting.</summary>
    private async Task PublishOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var query = scope.ServiceProvider.GetRequiredService<ILiveOeeQueryService>();
            var model = await query.GetAsync(new LiveOeeQuery(options.Value.StaleSeconds), ct);
            // Cache phục vụ circuit cùng area; SignalR phục vụ các client đang kết nối.
            state.Publish(model);
            await hub.Clients.All.LiveOeeUpdated(model);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception exception)
        {
            LogSourceUnavailable(logger, exception);
            // Không biến lỗi nguồn thành máy Stopped và không xóa KPI cuối đã xác nhận.
            var reconnecting = state.MarkReconnecting("Không thể cập nhật nguồn dữ liệu. Đang kết nối lại…", timeProvider.GetUtcNow());
            try { await hub.Clients.All.LiveOeeUpdated(reconnecting); }
            catch (Exception broadcastException) { LogBroadcastUnavailable(logger, broadcastException); }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime OEE publisher lost its data source; clients remain on the last confirmed values.")]
    private static partial void LogSourceUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not broadcast realtime reconnecting state.")]
    private static partial void LogBroadcastUnavailable(ILogger logger, Exception exception);
}
