using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oee.Application.Common;
using Oee.Application.PlcIntegration;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Application.PlcIntegration.Processing.Process;
using Oee.Domain.Common;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.BackgroundServices.Configuration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>
/// BackgroundService dùng một session/subscription cho mỗi connection và chuyển sample thành telemetry frame.
/// </summary>
/// <remarks>
/// Các thiết bị cùng connection dùng chung runner. Callback đẩy sample vào bounded Channel; một consumer tuần tự
/// gọi IPlcTelemetryProcessor để giảm race condition trên checkpoint/downtime. Worker không chứa công thức OEE.
/// </remarks>
public sealed partial class OpcUaIngestionWorker(IServiceScopeFactory scopeFactory, IOpcUaClientAdapter adapter, IOptions<OpcUaWorkerOptions> options, ILogger<OpcUaIngestionWorker> logger, IOpcTagMonitorBuffer? monitor = null) : BackgroundService
{
    // Bounded Channel áp dụng backpressure: khi consumer chậm, producer chờ thay vì tăng RAM không giới hạn.
    // Chụp frame trước khi đưa vào queue: consumer không đọc cache có thể đã đổi ở producer.
    private readonly Channel<DeviceTelemetryFrame> channel = Channel.CreateBounded<DeviceTelemetryFrame>(new BoundedChannelOptions(options.Value.ChannelCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> lastReceived = new();
    private readonly ConcurrentDictionary<Guid, (string Reason, DateTimeOffset At)> lastRejectionWarnings = new();
    private readonly HashSet<Guid> monitoredDevices = [];

    /// <summary>Điều phối vòng đời runner và consumer cho đến khi host gửi stoppingToken.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runners = new Dictionary<Guid, OpcUaConnectionRunner>();
        var consumer = ConsumeAsync(stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await ReconcileConnectionsAsync(runners, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { LogConfigurationUnavailable(logger, exception); }
                await Task.Delay(TimeSpan.FromSeconds(options.Value.ConfigurationRefreshSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            foreach (var runner in runners.Values) await runner.Stop.CancelAsync();
            foreach (var pair in runners) await StopRunnerAsync(pair.Key, pair.Value);
            channel.Writer.TryComplete();
            try { await consumer; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }

    private async Task ReconcileConnectionsAsync(Dictionary<Guid, OpcUaConnectionRunner> runners, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        var devices = await db.PlcDeviceMappings.AsNoTracking().Include(device => device.OpcUaConnection)
            .Where(device => device.IsActive && device.OpcUaConnection.IsActive).ToArrayAsync(cancellationToken);
        var ids = devices.Select(device => device.Id).ToArray();
        var tags = await db.OpcUaTagMappings.AsNoTracking()
            .Where(tag => ids.Contains(tag.PlcDeviceMappingId)).ToArrayAsync(cancellationToken);
        var configured = devices.Where(device => HasRequiredTags(tags.Where(tag => tag.PlcDeviceMappingId == device.Id))).ToArray();
        var activeIds = configured.Select(device => device.Id).ToHashSet();
        foreach (var removed in monitoredDevices.Where(id => !activeIds.Contains(id)).ToArray())
        {
            lastReceived.TryRemove(removed, out _);
            lastRejectionWarnings.TryRemove(removed, out _);
            monitor?.RemoveDevice(removed);
            monitoredDevices.Remove(removed);
        }
        monitoredDevices.UnionWith(activeIds);

        var groups = configured.GroupBy(device => device.OpcUaConnectionId).ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var removed in runners.Keys.Where(id => !groups.ContainsKey(id)).ToArray())
        {
            await runners[removed].Stop.CancelAsync();
            await StopRunnerAsync(removed, runners[removed]);
            runners.Remove(removed);
        }
        foreach (var group in groups)
        {
            var groupIds = group.Value.Select(device => device.Id).ToHashSet();
            // Tag RowVersion cũng thuộc signature: sửa riêng tag phải thay subscription ngay.
            var signature = Convert.ToHexString(group.Value[0].OpcUaConnection.RowVersion) + ":"
                + string.Join(':', group.Value.OrderBy(device => device.Id).Select(device => device.Id + Convert.ToHexString(device.RowVersion)))
                + string.Join(':', tags.Where(tag => groupIds.Contains(tag.PlcDeviceMappingId)).OrderBy(tag => tag.Id)
                    .Select(tag => tag.Id + Convert.ToHexString(tag.RowVersion)));
            if (runners.TryGetValue(group.Key, out var previous) && previous.Signature == signature && !previous.Task.IsCompleted)
                continue;
            if (previous is not null)
            {
                await previous.Stop.CancelAsync();
                await StopRunnerAsync(group.Key, previous);
            }
            var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            runners[group.Key] = new(signature, stop, RunConnectionAsync(group.Key, stop.Token));
        }
    }

    private async Task StopRunnerAsync(Guid connectionId, OpcUaConnectionRunner runner)
    {
        try { await runner.Task; }
        catch (OperationCanceledException) when (runner.Stop.IsCancellationRequested) { }
        catch (Exception exception) { LogRunnerStopped(logger, connectionId, exception.GetType().Name); }
        finally { runner.Stop.Dispose(); }
    }

    private async Task RunConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var delay = options.Value.InitialReconnectSeconds;
        Guid[] deviceIds = [];
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
                var connection = await db.OpcUaConnections.AsNoTracking().SingleAsync(item => item.Id == connectionId, cancellationToken);
                var devices = await db.PlcDeviceMappings.AsNoTracking()
                    .Where(device => device.OpcUaConnectionId == connectionId && device.IsActive).ToArrayAsync(cancellationToken);
                var ids = devices.Select(device => device.Id).ToArray();
                var tags = await db.OpcUaTagMappings.AsNoTracking()
                    .Where(tag => ids.Contains(tag.PlcDeviceMappingId)).ToArrayAsync(cancellationToken);
                var inputs = devices.Select(device => new OpcUaDeviceInput(device.Id, tags
                        .Where(tag => tag.PlcDeviceMappingId == device.Id && tag.Direction == TagDirectionEnum.Read)
                        .Select(tag => new OpcUaReadRequest(tag.NodeId, tag.TagRole)).ToArray()))
                    .Where(device => device.Reads.Any(read => read.Role == TagRoleEnum.Running)
                        && device.Reads.Any(read => read.Role == TagRoleEnum.TotalCounter)).ToArray();
                deviceIds = inputs.Select(device => device.Id).ToArray();
                if (deviceIds.Length == 0) return;
                var endpoint = new OpcUaEndpointOptions(connection.Id, connection.EndpointUrl, connection.SecurityPolicy,
                    connection.SecurityMode, connection.AuthenticationType, connection.UserName,
                    connection.CredentialSecretReference, connection.CertificateReference);
                await using var session = await adapter.ConnectAsync(endpoint, cancellationToken);
                var states = inputs.ToDictionary(device => device.Id, _ => new Dictionary<TagRoleEnum, OpcUaValueSample>());
                var health = deviceIds.ToDictionary(id => id, _ => ConnectivityStateEnum.Disconnected);
                using var routeGate = new SemaphoreSlim(1, 1);
                foreach (var device in inputs)
                {
                    lastReceived.TryRemove(device.Id, out _);
                    monitor?.Configure(device.Id, device.Reads);
                }
                delay = options.Value.InitialReconnectSeconds;
                var pump = new OpcUaConnectionPump(inputs);
                await pump.RunAsync(session, ReceiveAsync, CheckHealthAsync,
                    TimeSpan.FromSeconds(options.Value.ReadRefreshSeconds), cancellationToken,
                    TimeSpan.FromSeconds(options.Value.ReadTimeoutSeconds));

                async ValueTask ReceiveAsync(Guid deviceId, IReadOnlyCollection<OpcUaValueSample> samples,
                    bool initial, bool readRefresh, CancellationToken token)
                {
                    var hasDefect = inputs.Single(device => device.Id == deviceId).Reads.Any(read => read.Role == TagRoleEnum.DefectCounter);
                    await RouteAsync(deviceId, samples, states[deviceId], routeGate, hasDefect, initial, token, readRefresh);
                }

                async Task CheckHealthAsync(CancellationToken token)
                {
                    foreach (var device in inputs)
                    {
                        var snapshot = states[device.Id];
                        ConnectivityStateEnum next;
                        await routeGate.WaitAsync(token);
                        try
                        {
                            next = device.Reads.Any(read => !snapshot.ContainsKey(read.Role))
                                ? ConnectivityStateEnum.BadTag
                                : !lastReceived.TryGetValue(device.Id, out var at)
                                    || at < DateTimeOffset.UtcNow.AddSeconds(-options.Value.NoDataSeconds)
                                    ? ConnectivityStateEnum.NoData : ConnectivityStateEnum.Connected;
                        }
                        finally { routeGate.Release(); }
                        if (health[device.Id] == next) continue;
                        await MarkConnectivityAsync(device.Id, next, next.ToString(), token);
                        health[device.Id] = next;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                foreach (var deviceId in deviceIds)
                {
                    LogConnectionFailure(logger, deviceId, exception.GetType().Name);
                    try { await MarkConnectivityAsync(deviceId, ConnectivityStateEnum.Disconnected,
                        $"OPC UA connection failed ({exception.GetType().Name}).", cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                    catch (Exception persistenceError) { LogProcessingFailure(logger, deviceId, persistenceError); }
                }
                await Task.Delay(TimeSpan.FromSeconds(delay + Random.Shared.NextDouble()), cancellationToken);
                delay = Math.Min(delay * 2, options.Value.MaximumReconnectSeconds);
            }
        }
    }
    /// <summary>Kiểm tra quality, cập nhật cache của session và chụp frame bất biến vào queue.</summary>
    private async ValueTask RouteAsync(Guid deviceId, IReadOnlyCollection<OpcUaValueSample> samples,
        Dictionary<TagRoleEnum, OpcUaValueSample> values, SemaphoreSlim routeGate, bool hasDefectTag,
        bool reconnect, CancellationToken ct, bool isReadRefresh = false)
    {
        await routeGate.WaitAsync(ct);
        try
        {
            foreach (var sample in samples)
            {
                // Chẩn đoán giữ cả Bad/null và timestamp nhận thực tế, trước mọi lọc/ép kiểu nghiệp vụ.
                monitor?.Record(deviceId, sample, DateTimeOffset.UtcNow, isReadRefresh);
                if (!sample.IsGood || sample.Value is null)
                {
                    values.Remove(sample.Role);
                    monitor?.SetConnection(deviceId, ConnectivityStateEnum.BadTag.ToString());
                    continue;
                }
                // Callback song song có thể đến muộn: không ghi đè giá trị mới bằng sample cũ.
                if (values.TryGetValue(sample.Role, out var previous) && sample.SourceTimestamp < previous.SourceTimestamp) continue;
                values[sample.Role] = sample;
            }
            if (!values.TryGetValue(TagRoleEnum.Running, out var running) || !values.TryGetValue(TagRoleEnum.TotalCounter, out var total)) return;
            values.TryGetValue(TagRoleEnum.DefectCounter, out var defect);
            if (hasDefectTag && defect is null) return;
            var receivedAt = DateTimeOffset.UtcNow;
            var timestamp = new[] { running.SourceTimestamp, total.SourceTimestamp, defect?.SourceTimestamp ?? total.SourceTimestamp }.Max();
            var frame = new DeviceTelemetryFrame(deviceId,
                Convert.ToBoolean(running.Value, CultureInfo.InvariantCulture),
                Convert.ToDecimal(total.Value, CultureInfo.InvariantCulture),
                defect is null ? 0 : Convert.ToDecimal(defect.Value, CultureInfo.InvariantCulture),
                values.TryGetValue(TagRoleEnum.FaultCode, out var fault) ? Convert.ToString(fault.Value, CultureInfo.InvariantCulture) : null,
                values.TryGetValue(TagRoleEnum.ProductCode, out var product) ? Convert.ToString(product.Value, CultureInfo.InvariantCulture) : null,
                timestamp, FormattableString.Invariant($"{timestamp.UtcTicks}:{Convert.ToDecimal(total.Value, CultureInfo.InvariantCulture)}:{(defect is null ? 0 : Convert.ToDecimal(defect.Value, CultureInfo.InvariantCulture))}"),
                reconnect, receivedAt, isReadRefresh, total.SourceTimestamp, running.SourceTimestamp);
            lastReceived[deviceId] = receivedAt;
            await channel.Writer.WriteAsync(frame, ct);
        }
        finally { routeGate.Release(); }
    }

    /// <summary>Ghép sample rời thành DeviceTelemetryFrame rồi gọi processor theo một luồng consumer.</summary>
    private async Task ConsumeAsync(CancellationToken ct)
    {
        var processor = new PlcTelemetryFrameProcessor(ProcessFrameAsync);
        await foreach (var frame in channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var result = await processor.ProcessAsync(frame, ct);
                // Lượt đọc thành công không có nghĩa counter đã được chấp nhận. Ghi rõ
                // lý do giữ checkpoint, tối đa một lần/phút/device/lý do liên tiếp.
                if (!result.Accepted && result.Reason is { } reason && reason != "DuplicateSampleKey")
                {
                    var now = DateTimeOffset.UtcNow;
                    if (!lastRejectionWarnings.TryGetValue(frame.DeviceMappingId, out var previous)
                        || previous.Reason != reason || now - previous.At >= TimeSpan.FromMinutes(1))
                    {
                        lastRejectionWarnings[frame.DeviceMappingId] = (reason, now);
                        LogCounterRejected(logger, frame.DeviceMappingId, reason, frame.TotalCounter, frame.DefectCounter,
                            frame.SourceTimestamp, frame.TotalCounterTimestamp, frame.ReceivedAt);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception exception) { LogProcessingFailure(logger, frame.DeviceMappingId, exception); }
        }

        async Task<PlcProcessingResult> ProcessFrameAsync(DeviceTelemetryFrame frame, CancellationToken token)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>()
                .ExecuteAsync(new ProcessPlcTelemetryCommand(frame), token);
        }
    }

    /// <summary>Persist connectivity/session event để các query đọc trạng thái trong SQL.</summary>
    /// <remarks>Connectivity và OperationalState là hai trục độc lập; method này chỉ cập nhật connectivity.</remarks>
    private async Task MarkConnectivityAsync(Guid deviceId, ConnectivityStateEnum state, string message, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        monitor?.SetConnection(deviceId, state.ToString());
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        var connectionId = await db.PlcDeviceMappings.Where(x => x.Id == deviceId).Select(x => x.OpcUaConnectionId).SingleAsync(ct);
        var runtime = await db.PlcDeviceRuntimeStates.SingleOrDefaultAsync(x => x.PlcDeviceMappingId == deviceId, ct);
        if (runtime is null)
        {
            runtime = PlcDeviceRuntimeStateEntity.Create(deviceId);
            db.PlcDeviceRuntimeStates.Add(runtime);
        }
        runtime.MarkConnectivity(state);
        var at = DateTimeOffset.UtcNow;
        var level = state == ConnectivityStateEnum.Connected ? IntegrationEventLevelEnum.Information : IntegrationEventLevelEnum.Warning;
        db.PlcIntegrationEvents.Add(PlcIntegrationEventEntity.Create(at, level, "Connectivity", message, deviceId));
        db.PlcSessionEvents.Add(PlcSessionEventEntity.Create(connectionId, at, state, message));
        await db.SaveChangesAsync(ct);
    }
    private static bool HasRequiredTags(IEnumerable<Domain.PlcIntegration.OpcUaTagMappingEntity> tags) { var roles = tags.Where(tag => tag.Direction == TagDirectionEnum.Read).Select(tag => tag.TagRole).ToHashSet(); return roles.Contains(TagRoleEnum.Running) && roles.Contains(TagRoleEnum.TotalCounter); }
    [LoggerMessage(Level = LogLevel.Warning, Message = "OPC UA connection failed for device {DeviceId}; error type {ErrorType}")] private static partial void LogConnectionFailure(ILogger logger, Guid deviceId, string errorType);
    [LoggerMessage(Level = LogLevel.Error, Message = "OPC UA sample processing failed for device {DeviceId}")] private static partial void LogProcessingFailure(ILogger logger, Guid deviceId, Exception exception);
    [LoggerMessage(Level = LogLevel.Warning, Message = "OPC UA device runner {DeviceId} stopped; error type {ErrorType}")] private static partial void LogRunnerStopped(ILogger logger, Guid deviceId, string errorType);
    [LoggerMessage(Level = LogLevel.Error, Message = "OPC UA configuration refresh failed; Web remains available and will retry")]
    private static partial void LogConfigurationUnavailable(ILogger logger, Exception exception);
    [LoggerMessage(Level = LogLevel.Warning, Message = "OPC UA counter rejected for device {DeviceId}: {Reason}; total {TotalCounter}, defect {DefectCounter}, frame source {SourceAt}, total source {CounterAt}, received {ReceivedAt}; checkpoint retained")]
    private static partial void LogCounterRejected(ILogger logger, Guid deviceId, string reason, decimal totalCounter, decimal defectCounter,
        DateTimeOffset sourceAt, DateTimeOffset? counterAt, DateTimeOffset? receivedAt);
}
