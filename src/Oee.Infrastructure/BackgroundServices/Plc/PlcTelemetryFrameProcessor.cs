using Oee.Application.Common;
using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>Giữ dấu reconnect cho tới khi xử lý thành công và thử lại khi RowVersion xung đột.</summary>
/// <remarks>Chỉ gọi từ consumer tuần tự. Mỗi lần process phải tạo scope lưu dữ liệu mới.</remarks>
internal sealed class PlcTelemetryFrameProcessor(
    Func<DeviceTelemetryFrame, CancellationToken, Task<PlcProcessingResult>> process)
{
    private readonly HashSet<Guid> reconnectPending = [];

    internal async Task<PlcProcessingResult> ProcessAsync(DeviceTelemetryFrame frame, CancellationToken cancellationToken)
    {
        if (frame.IsReconnect)
            reconnectPending.Add(frame.DeviceMappingId);

        var effectiveFrame = reconnectPending.Contains(frame.DeviceMappingId)
            ? frame with { IsReconnect = true }
            : frame;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var result = await process(effectiveFrame, cancellationToken);
                // Accepted đã chốt checkpoint, hoặc marker giảm counter đã được lưu.
                // Duplicate/out-of-order chưa chứng minh sample reconnect được xử lý.
                if (result.Accepted || result.Reason is "ReconnectCounterDecrease" or "UnexpectedCounterDecrease" or "CounterRecoveryJump")
                    reconnectPending.Remove(frame.DeviceMappingId);

                return result;
            }
            catch (ConcurrencyConflictException) when (attempt < 2)
            {
                // Scope mới tải lại RowVersion. Không bỏ mẫu đầu reconnect vì runtime
                // có thể được health-check cập nhật đồng thời với consumer.
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken);
            }
        }
    }
}
