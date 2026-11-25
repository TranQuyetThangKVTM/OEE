using System.Text.Json;
using Oee.Application.Common;
using Oee.Application.Operations;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;
using Oee.Domain.Production;

namespace Oee.Application.PlcIntegration.Processing.Process;

/// <summary>Điều phối thao tác ProcessPlcTelemetry trong cùng scope lưu dữ liệu.</summary>
internal sealed class ProcessPlcTelemetryHandler(
    IPlcTelemetryStore store,
    IProductionOutputStore outputs,
    IDowntimeStore downtimes,
    IApplicationUnitOfWork unitOfWork,
    IClock clock)
    : IRequestHandler<ProcessPlcTelemetryCommand, PlcProcessingResult>
{
    public async Task<PlcProcessingResult> HandleAsync(
        ProcessPlcTelemetryCommand command,
        CancellationToken cancellationToken)
    {
        var frame = command.Frame; // SampleKey trùng bị từ chối trước khi cộng delta hoặc ghi fact mới.
        var device = await store.FindDeviceAsync(frame.DeviceMappingId, cancellationToken)
            ?? throw new KeyNotFoundException("PLC device mapping was not found.");
        var checkpoint = await store.FindCheckpointAsync(device.Id, cancellationToken);
        var checkpointIsNew = checkpoint is null;
        var runtime = await store.FindRuntimeAsync(device.Id, cancellationToken);
        var previousObservedAt = runtime?.LastDataAt ?? checkpoint?.LastReadAt;
        // Vận hành và counter độc lập: counter bị giữ mốc vẫn phải cập nhật chạy/dừng.
        // Replay không được dùng lại lượt nhận cũ để tăng debounce hoặc tạo downtime mới.
        var receivedAt = frame.ReceivedAt ?? frame.SourceTimestamp;
        var observed = frame.IsRunning ? OperationalStateEnum.Running : OperationalStateEnum.Stopped;

        if (runtime is null)
        {
            runtime = PlcDeviceRuntimeStateEntity.Create(
                plcDeviceMappingId: device.Id,
                connectivityState: ConnectivityStateEnum.Connected,
                operationalState: observed,
                lastDataAt: receivedAt,
                lastFaultCode: device.NormalizeFault(frame.FaultCode));
            store.AddRuntime(runtime);
        }
        else if (!runtime.LastDataAt.HasValue || receivedAt > runtime.LastDataAt)
            await ApplyOperationalStateAsync(runtime, device, observed, frame, cancellationToken);

        if (checkpoint?.IsDuplicateSample(frame.SampleKey) == true)
        {
            if (unitOfWork.HasChanges)
                await unitOfWork.SaveChangesAsync(cancellationToken);

            return new(false, 0, 0, false, "DuplicateSampleKey");
        }

        var state = checkpoint?.State ?? new CounterStateValue(null, null, null, 0);
        var counterDiscontinuityPending = checkpoint?.IsDiscontinuityPending == true;
        // Run/Stop vẫn có thể được nhận trong lúc counter trả 0 hoặc giảm bất thường.
        // Các lượt đó không chứng minh sản lượng đã được quan sát liên tục qua ranh giới ca.
        // Khi counter phục hồi, dùng mốc counter đã chấp nhận để không che mất khoảng gián đoạn.
        if (counterDiscontinuityPending && checkpoint?.LastReadAt is { } lastAcceptedAt
            && (!previousObservedAt.HasValue || lastAcceptedAt < previousObservedAt.Value))
            previousObservedAt = lastAcceptedAt;
        var delta = CounterDeltaCalculator.Calculate(
            state,
            new CounterSampleValue(frame.TotalCounter, frame.DefectCounter, frame.SourceTimestamp, frame.SampleKey),
            device.CounterMode,
            isReconnect: frame.IsReconnect || counterDiscontinuityPending);

        // Sau khi lập mốc thấp mới, counter cũ có thể xuất hiện lại muộn. Không cộng
        // bước nhảy đó thành sản lượng; phải đọc trực tiếp và xác nhận dãy mới lần nữa.
        if (checkpoint?.RequiresRecoveryRead(delta, receivedAt) == true)
            delta = new(false, 0, 0, state, "CounterRecoveryJump");

        if (!delta.Accepted)
        {
            if (delta.Reason is "UnexpectedCounterDecrease" or "ReconnectCounterDecrease" or "CounterRecoveryJump" && checkpoint is not null)
            {
                if (!counterDiscontinuityPending)
                    AddEvent(
                        device.Id,
                        "CounterDiscontinuity",
                        FormattableString.Invariant($"Counter discontinuity; checkpoint retained, no production added. Total {state.LastTotal} -> {frame.TotalCounter}; defect {state.LastDefect} -> {frame.DefectCounter}. Direct read confirmation will resume counting from a stable new baseline."),
                        frame.SourceTimestamp,
                        IntegrationEventLevelEnum.Warning);
                checkpoint.MarkDiscontinuity();
                var recovered = checkpoint.ObserveRecovery(
                    new(frame.TotalCounter, frame.DefectCounter, frame.TotalCounterTimestamp ?? frame.SourceTimestamp, frame.SampleKey),
                    receivedAt, frame.IsReadRefresh, frame.IsReconnect);
                if (recovered is not null)
                {
                    delta = recovered;
                    AddEvent(device.Id, "CounterRecovered", JsonSerializer.Serialize(new
                    {
                        PreviousTotal = state.LastTotal, PreviousDefect = state.LastDefect,
                        NewTotal = frame.TotalCounter, NewDefect = frame.DefectCounter,
                        From = state.LastTimestamp, Until = receivedAt,
                        Message = "Fresh direct reads confirmed a stable new counter stream. Baseline only; no historical production assigned."
                    }), receivedAt, IntegrationEventLevelEnum.Warning);
                }
            }

            if (!delta.Accepted)
            {
                if (unitOfWork.HasChanges)
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                return new(false, 0, 0, false, delta.Reason);
            }
        }

        // Checkpoint mới/hiện có được cập nhật cùng transaction với output/quarantine.
        checkpoint ??= CounterCheckpointEntity.Create(plcDeviceMappingId: device.Id);
        checkpoint.Apply(delta, frame.SampleKey, frame.ReceivedAt ?? clock.UtcNow);

        if (checkpointIsNew)
            store.AddCheckpoint(checkpoint);

        // Raw frame là bằng chứng đã xử lý trong cùng transaction với checkpoint/output.
        // Không đưa frame đã commit vào hàng đợi replay để tránh phát lại counter cũ.
        var raw = OutboxMessageEntity.Create(
            idempotencyKey: $"plc:{device.Id:N}:{frame.SampleKey}",
            occurredAt: frame.SourceTimestamp,
            type: "PlcTelemetryFrame",
            payload: JsonSerializer.Serialize(frame),
            expiresAt: clock.UtcNow.AddHours(72));

        raw.MarkProcessed(clock.UtcNow);
        store.AddOutbox(raw);
        var quarantined = false;

        if (delta.TotalDelta > 0 || delta.DefectDelta > 0)
            quarantined = await PersistOutputOrUnplannedDeltaAsync(
                device, frame, delta, state.LastTimestamp, previousObservedAt, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new(true, delta.TotalDelta, delta.DefectDelta, quarantined, delta.Reason);
    }

    /// <summary>Áp dụng source timestamp, reconnect restore và debounce trước transition.</summary>
    private async Task ApplyOperationalStateAsync(
        PlcDeviceRuntimeStateEntity runtime,
        PlcDeviceMappingEntity device,
        OperationalStateEnum observed,
        DeviceTelemetryFrame frame,
        CancellationToken cancellationToken)
    {
        var sourceAt = frame.RunningTimestamp ?? frame.SourceTimestamp;
        var previous = runtime.Observe(
            observed,
            sourceAt,
            frame.ReceivedAt ?? frame.SourceTimestamp,
            frame.IsReconnect,
            device.NormalizeFault(frame.FaultCode),
            device.DebounceSamples);

        if (!previous.HasValue)
        {
            // Reconnect khôi phục trạng thái mà không tạo transition giả, nhưng một
            // downtime đã biết phải được đóng khi Run xác nhận máy đã chạy trở lại.
            if (frame.IsReconnect && observed == OperationalStateEnum.Running)
            {
                var open = await store.FindOpenDowntimeAsync(device.WorkUnitId, cancellationToken);

                if (open is not null && sourceAt > open.StartAt)
                {
                    open.Close(sourceAt);
                    AddEvent(
                        device.Id,
                        "StateRestored",
                        "Reconnect confirmed running; closed the existing downtime.",
                        sourceAt);
                }
            }

            return;
        }

        if (previous == OperationalStateEnum.Running && observed == OperationalStateEnum.Stopped)
            await OpenDowntimeAsync(device, frame, cancellationToken);

        if (previous == OperationalStateEnum.Stopped && observed == OperationalStateEnum.Running)
        {
            var open = await store.FindOpenDowntimeAsync(device.WorkUnitId, cancellationToken);

            if (open is not null)
                open.Close(sourceAt);
        }

        AddEvent(device.Id, "StateTransition", $"{previous.Value} -> {observed}", sourceAt);
    }

    /// <summary>Mở downtime sau transition đã debounce và chọn reason device-specific rồi global.</summary>
    private async Task OpenDowntimeAsync(
        PlcDeviceMappingEntity device,
        DeviceTelemetryFrame frame,
        CancellationToken cancellationToken)
    {
        if (await downtimes.HasOpenAsync(device.WorkUnitId, cancellationToken))
            return;
        var fault = device.NormalizeFault(frame.FaultCode);
        // OrderBy WorkUnitId.HasValue ưu tiên mapping riêng; fallback cuối là DR-UNKNOWN.
        var reasonId = await store.FindFaultReasonAsync(device.WorkUnitId, fault, cancellationToken)
            ?? await store.FindUnknownReasonAsync(cancellationToken)
            ?? throw new InvalidOperationException("DR-UNKNOWN must exist for unmatched PLC faults.");
        var reason = await downtimes.FindReasonAsync(reasonId, cancellationToken)
            ?? throw new KeyNotFoundException("Downtime reason was not found.");
        downtimes.Add(DowntimeEventEntity.Create(
                workUnitId: device.WorkUnitId,
                downtimeReasonId: reason.Id,
                category: reason.Category,
                kind: reason.Kind,
                startAt: frame.RunningTimestamp ?? frame.SourceTimestamp,
                source: DataSourceEnum.Automatic,
                note: fault is null ? "PLC stopped without fault code" : $"PLC fault {fault}"));

        if (fault is not null && !await store.HasFaultMappingAsync(device.WorkUnitId, fault, cancellationToken))
            AddEvent(device.Id, "UnclassifiedFault", $"Fault {fault} mapped to DR-UNKNOWN", frame.SourceTimestamp);
    }

    /// <summary>Tạo output khi có plan đủ bằng chứng; ngược lại giữ delta trong quarantine.</summary>
    private async Task<bool> PersistOutputOrUnplannedDeltaAsync(
        PlcDeviceMappingEntity device,
        DeviceTelemetryFrame frame,
        CounterDeltaResultValue delta,
        DateTimeOffset? previousTimestamp,
        DateTimeOffset? previousObservedAt,
        CancellationToken cancellationToken)
    {
        // Run/Defect có thể mang timestamp mới hơn Total khi đổi ca/reconnect.
        // Sản lượng phải theo thời điểm counter tổng đổi, không theo timestamp lớn nhất của cả frame.
        var producedAt = frame.TotalCounterTimestamp ?? frame.SourceTimestamp;
        var plan = await store.FindPlanAtAsync(device.WorkUnitId, producedAt, cancellationToken);

        if (plan is null)
        {
            store.AddUnplannedDelta(ProductionReconciliationItemEntity.Create(
                    workUnitId: device.WorkUnitId,
                    sourceKey: frame.SampleKey,
                    capturedAt: producedAt,
                    totalDelta: delta.TotalDelta,
                    defectDelta: delta.DefectDelta,
                    reasonCode: "NO_ACTIVE_PLAN",
                    productId: device.DefaultProductId));
            AddEvent(device.Id, "Quarantine", "Counter delta quarantined: no active plan", producedAt);

            return true;
        }

        if (ProductionOutputAttributionRules.IsUncertainAfterCollectionGap(
            previousObservedAt, frame.ReceivedAt ?? clock.UtcNow, producedAt, plan.PlannedStart))
        {
            // Hai counter chỉ chứng minh tổng chênh lệch, không chứng minh phần nào thuộc mỗi ca.
            // Giữ delta riêng và tiến checkpoint cùng transaction; mẫu sau chỉ cộng phần mới.
            store.AddUnplannedDelta(ProductionReconciliationItemEntity.Create(
                device.WorkUnitId, frame.SampleKey, producedAt, delta.TotalDelta, delta.DefectDelta,
                ProductionOutputAttributionRules.CollectionGapReason, plan.ProductId));
            AddEvent(device.Id, "CollectionGap", JsonSerializer.Serialize(new
            {
                From = previousObservedAt,
                Until = frame.ReceivedAt ?? clock.UtcNow,
                PreviousCounterAt = previousTimestamp,
                CounterAt = producedAt,
                PlanStart = plan.PlannedStart,
                delta.TotalDelta,
                delta.DefectDelta
            }), producedAt, IntegrationEventLevelEnum.Warning);
            return true;
        }

        var speed = await outputs.ReadEffectiveSpeedAsync(
            device.WorkUnitId,
            plan.ProductId,
            DateOnly.FromDateTime(producedAt.UtcDateTime),
            cancellationToken)
            ?? throw new InvalidOperationException("No effective standard for automatic output.");
        var start = previousTimestamp.HasValue && previousTimestamp < producedAt
            ? previousTimestamp.Value
            : producedAt.AddMilliseconds(-1);
        if (start < plan.PlannedStart && producedAt > plan.PlannedStart)
            start = plan.PlannedStart;
        outputs.Add(ProductionOutputEntity.Create(
                productionPlanId: plan.Id,
                workUnitId: device.WorkUnitId,
                productId: plan.ProductId,
                periodStart: start,
                periodEnd: producedAt,
                standardSpeedPerMinute: speed,
                totalProducedQuantity: delta.TotalDelta,
                source: DataSourceEnum.Automatic,
                defectQuantity: delta.DefectDelta,
                shiftId: plan.ShiftId,
                sourceNote: $"PLC:{frame.SampleKey}"));

        return false;
    }

    /// <summary>Ghi sự kiện kỹ thuật vào SQL ChangeTracker.</summary>
    private void AddEvent(
        Guid deviceId,
        string kind,
        string message,
        DateTimeOffset at,
        IntegrationEventLevelEnum level = IntegrationEventLevelEnum.Information)
    {
        store.AddEvent(PlcIntegrationEventEntity.Create(
                occurredAt: at,
                level: level,
                kind: kind,
                message: message,
                plcDeviceMappingId: deviceId));
    }
}
