using Microsoft.Extensions.Logging;
using Oee.Application.PlcIntegration;
using Opc.Ua;
using Opc.Ua.Client;

namespace Oee.Infrastructure.Integrations.OpcUa;

internal sealed class OpcUaSdkSession(ISession session, int publishingIntervalMs, ILogger<OpcUaSdkAdapter> logger) : IOpcUaSession
{
    private readonly OpcUaKeepAliveState keepAlive = new();
    private bool observingKeepAlive;
    /// <summary>Trạng thái socket/session do SDK cung cấp.</summary>
    public bool IsConnected => session.Connected && !keepAlive.ReconnectRequested;

    /// <summary>Browse node con của một NodeId, mặc định bắt đầu từ ObjectsFolder.</summary>
    public async Task<IReadOnlyCollection<OpcUaNodeDescriptor>> BrowseAsync(string? parentNodeId, CancellationToken cancellationToken)
    {
        var browser = new Browser(session);
        var references = await browser.BrowseAsync(string.IsNullOrWhiteSpace(parentNodeId) ? ObjectIds.ObjectsFolder : NodeId.Parse(parentNodeId), cancellationToken);
        var nodes = references.Where(reference => reference.NodeClass is NodeClass.Object or NodeClass.View or NodeClass.Variable)
            .Select(reference => (Reference: reference, Id: ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris)))
            .Where(node => node.Id is not null).ToArray();
        var variables = nodes.Where(node => node.Reference.NodeClass == NodeClass.Variable).ToArray();
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        if (variables.Length > 0)
        {
            // TypeDefinition là loại node, không phải kiểu dữ liệu của biến. Đọc Attribute DataType theo batch.
            var attributes = new ReadValueIdCollection(variables.Select(node => new ReadValueId { NodeId = node.Id, AttributeId = Attributes.DataType }));
            var response = await session.ReadAsync(null, 0, TimestampsToReturn.Neither, attributes, cancellationToken);
            for (var i = 0; i < variables.Length; i++)
                if (StatusCode.IsGood(response.Results[i].StatusCode) && response.Results[i].Value is NodeId dataType)
                    types[variables[i].Id.ToString()] = TypeInfo.GetBuiltInType(dataType, session.TypeTree).ToString();
        }
        return nodes.Select(node => new OpcUaNodeDescriptor(node.Id.ToString(), node.Reference.DisplayName.Text,
            types.GetValueOrDefault(node.Id.ToString()), node.Reference.NodeClass is NodeClass.Object or NodeClass.View,
            node.Reference.NodeClass == NodeClass.Variable)).ToArray();
    }

    /// <summary>Đọc lần lượt các node và giữ source timestamp/status code của PLC.</summary>
    public async Task<IReadOnlyCollection<OpcUaValueSample>> ReadAsync(IReadOnlyCollection<OpcUaReadRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0) return [];
        ObserveKeepAlive();
        var batch = requests.ToArray();
        var nodes = new ReadValueIdCollection(batch.Select(request => new ReadValueId
        {
            NodeId = NodeId.Parse(request.NodeId), AttributeId = Attributes.Value
        }));
        var response = await session.ReadAsync(null, 0, TimestampsToReturn.Both, nodes, cancellationToken);
        return batch.Select((request, index) => new OpcUaValueSample(request.NodeId, request.Role,
            response.Results[index].Value, response.Results[index].SourceTimestamp,
            StatusCode.IsGood(response.Results[index].StatusCode), response.Results[index].StatusCode.ToString())).ToArray();
    }

    /// <summary>Gửi một batch giá trị write-back và báo lỗi nếu bất kỳ status code nào là Bad.</summary>
    public async Task WriteAsync(IReadOnlyCollection<OpcUaWriteRequest> requests, CancellationToken cancellationToken)
    {
        var values = new WriteValueCollection(requests.Select(request => new WriteValue { NodeId = NodeId.Parse(request.NodeId), AttributeId = Attributes.Value, Value = new DataValue(new Variant(request.Value)) }));
        var response = await session.WriteAsync(null, values, cancellationToken); for (var i = 0; i < response.Results.Count; i++) if (StatusCode.IsBad(response.Results[i])) throw new ServiceResultException(response.Results[i], $"Write failed for {requests.ElementAt(i).NodeId}");
    }

    /// <summary>Tạo monitored item cho từng read tag và chuyển notification thành sample trung lập.</summary>
    /// <remarks>
    /// PublishingInterval là nhịp server phát batch, còn SamplingInterval là nhịp lấy mẫu của từng tag.
    /// SourceTimestamp của notification được giữ lại để lớp xử lý có thể loại sample cũ/out-of-order.
    /// </remarks>
    public async Task<IOpcUaSubscription> SubscribeAsync(IReadOnlyCollection<OpcUaReadRequest> requests, Func<OpcUaValueSample, CancellationToken, ValueTask> onSample, CancellationToken cancellationToken)
    {
        ObserveKeepAlive();
        var subscription = new Subscription(session.DefaultSubscription)
        {
            PublishingInterval = publishingIntervalMs, KeepAliveCount = 10, LifetimeCount = 100,
            MaxNotificationsPerPublish = 1000, PublishingEnabled = true, TimestampsToReturn = TimestampsToReturn.Both
        };
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var notificationToken = stop.Token;
        foreach (var request in requests)
        {
            var item = new MonitoredItem(subscription.DefaultItem)
            {
                StartNodeId = NodeId.Parse(request.NodeId), AttributeId = Attributes.Value,
                DisplayName = request.Role.ToString(), SamplingInterval = SamplingInterval(request.Role),
                QueueSize = 100, DiscardOldest = true, Handle = request
            };
            item.Notification += (sender, args) =>
            {
                var sample = OpcUaSdkAdapter.DecodeNotification(request, args.NotificationValue);
                if (sample is not null) _ = ForwardAsync(sample, onSample, notificationToken);
            };
            subscription.AddItem(item);
        }
        try
        {
            session.AddSubscription(subscription);
            await subscription.CreateAsync(cancellationToken);
            await subscription.ApplyChangesAsync(cancellationToken);
            foreach (var item in subscription.MonitoredItems)
            {
                if (item.Status.Error is { } error && ServiceResult.IsBad(error) && item.Handle is OpcUaReadRequest request)
                    await onSample(new(request.NodeId, request.Role, null, DateTimeOffset.UtcNow,
                        false, error.StatusCode.ToString()), notificationToken);
            }
            return new OpcUaSdkSubscription(subscription, stop);
        }
        catch { stop.Cancel(); stop.Dispose(); subscription.Dispose(); throw; }
    }

    private async Task ForwardAsync(OpcUaValueSample sample, Func<OpcUaValueSample, CancellationToken, ValueTask> onSample, CancellationToken ct)
    {
        try { await onSample(sample, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception exception) { OpcUaSdkAdapter.LogNotificationFailure(logger, exception.GetType().Name); }
    }

    /// <summary>Đóng session rồi giải phóng tài nguyên native/network của SDK.</summary>
    public async ValueTask DisposeAsync()
    {
        if (observingKeepAlive) session.KeepAlive -= OnKeepAlive;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await session.CloseAsync(timeout.Token).WaitAsync(timeout.Token); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        finally { session.Dispose(); }
    }

    private void ObserveKeepAlive()
    {
        if (observingKeepAlive) return;
        session.KeepAlive += OnKeepAlive;
        observingKeepAlive = true;
    }

    private void OnKeepAlive(ISession sender, KeepAliveEventArgs args) => keepAlive.Observe(ServiceResult.IsBad(args.Status));

    internal static int SamplingInterval(Oee.Domain.Common.TagRoleEnum role) => role switch
    {
        Oee.Domain.Common.TagRoleEnum.Running => 100,
        Oee.Domain.Common.TagRoleEnum.FaultCode => 200,
        Oee.Domain.Common.TagRoleEnum.ProductCode => 5000,
        _ => 1000
    };
}
