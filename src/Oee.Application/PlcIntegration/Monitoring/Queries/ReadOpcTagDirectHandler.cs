using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

/// <summary>Chỉ đọc một tag đã lưu; không gọi processor, không rebase và không cập nhật cache subscription.</summary>
internal sealed class ReadOpcTagDirectHandler(OpcTagMonitorContext context)
    : IRequestHandler<ReadOpcTagDirectQuery, OpcTagDirectRead>
{
    public async Task<OpcTagDirectRead> HandleAsync(
        ReadOpcTagDirectQuery request,
        CancellationToken cancellationToken)
    {
        context.EnsureReadAccess();
        var target = await context.Store.FindAsync(request.TagId, cancellationToken)
            ?? throw new InvalidOperationException("Tag đã bị xóa. Hãy tải lại ánh xạ.");
        var tag = target.Mapping;

        if (tag.NodeId != request.ExpectedNodeId)
            throw new InvalidOperationException("NodeId đã thay đổi. Hãy tải lại ánh xạ.");

        if (!tag.Enabled)
            throw new InvalidOperationException("Ánh xạ hoặc kết nối đang tắt.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await using var session = await context.Adapter.ConnectAsync(target.Endpoint, timeout.Token);
            var values = await session.ReadAsync([new(tag.NodeId, tag.Role)], timeout.Token);
            var sample = values.FirstOrDefault(value => value.NodeId == tag.NodeId && value.Role == tag.Role);

            return sample is null
                ? new(tag.NodeId, "—", "—", false, "Không có kết quả đọc", null, context.Clock.UtcNow)
                : new(
                tag.NodeId,
                OpcTagValueFormatter.Format(sample.Value),
                sample.Value?.GetType().Name ?? "—",
                sample.IsGood && sample.Value is not null,
                sample.Value is null
                    ? (sample.StatusCode ?? "Unknown") + " · Giá trị rỗng"
                    : sample.StatusCode ?? (sample.IsGood ? "Good" : "Bad"),
                sample.SourceTimestamp,
                context.Clock.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new(tag.NodeId, "—", "—", false, "Đọc quá thời gian chờ (10 giây)", null, context.Clock.UtcNow);
        }
        catch
        {
            return new(
                tag.NodeId,
                "—",
                "—",
                false,
                "Không thể đọc OPC UA. Kiểm tra kết nối và security.",
                null,
                context.Clock.UtcNow);
        }
    }
}
