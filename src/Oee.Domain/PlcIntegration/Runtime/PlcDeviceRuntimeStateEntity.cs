using Oee.Domain.Common;

namespace Oee.Domain.PlcIntegration;

/// <summary>Trạng thái kết nối/vận hành hiện tại và trạng thái chờ debounce của một device mapping.</summary>
/// <remarks>RowVersion bảo vệ hai worker không ghi đè state/checkpoint của nhau.</remarks>
public sealed class PlcDeviceRuntimeStateEntity : Entity
{
    public Guid PlcDeviceMappingId { get; private set; }
    public PlcDeviceMappingEntity PlcDeviceMapping { get; private set; } = null!;
    public ConnectivityStateEnum ConnectivityState { get; private set; }
    public OperationalStateEnum OperationalState { get; private set; }
    public DateTimeOffset? LastDataAt { get; private set; }
    public DateTimeOffset? LastTransitionAt { get; private set; }
    public OperationalStateEnum? PendingOperationalState { get; private set; }
    public int PendingSampleCount { get; private set; }
    public string? LastFaultCode { get; private set; }
    private byte[] rowVersion = [];
    public byte[] RowVersion
    {
        get => rowVersion.ToArray();
        private set => rowVersion = value.ToArray();
    }

    private PlcDeviceRuntimeStateEntity()
    {
    }

    public static PlcDeviceRuntimeStateEntity Create(Guid plcDeviceMappingId, ConnectivityStateEnum connectivityState = ConnectivityStateEnum.NotConfigured,
        OperationalStateEnum operationalState = OperationalStateEnum.Unknown, DateTimeOffset? lastDataAt = null, string? lastFaultCode = null)
    {
        return new PlcDeviceRuntimeStateEntity
        {
            PlcDeviceMappingId = DomainGuard.RequiredId(plcDeviceMappingId),
            ConnectivityState = DomainGuard.Defined(connectivityState),
            OperationalState = DomainGuard.Defined(operationalState),
            LastDataAt = lastDataAt?.ToUniversalTime(),
            LastFaultCode = DomainGuard.Optional(lastFaultCode, 64)
        };
    }

    public void MarkConnectivity(ConnectivityStateEnum state) => ConnectivityState = DomainGuard.Defined(state);

    public void RecordSuccessfulRead(DateTimeOffset receivedAt)
    {
        if (!LastDataAt.HasValue || receivedAt > LastDataAt) LastDataAt = receivedAt.ToUniversalTime();
        ConnectivityState = ConnectivityStateEnum.Connected;
    }

    /// <summary>Trả trạng thái trước đó chỉ khi transition được debounce; reconnect chỉ khôi phục trạng thái.</summary>
    public OperationalStateEnum? Observe(OperationalStateEnum observed, DateTimeOffset sourceAt, DateTimeOffset receivedAt,
        bool isReconnect, string? faultCode, int debounceSamples)
    {
        DomainGuard.Defined(observed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(debounceSamples);
        var normalizedFault = DomainGuard.Optional(faultCode, 64);
        RecordSuccessfulRead(receivedAt);
        LastFaultCode = normalizedFault;
        if (isReconnect)
        {
            OperationalState = observed;
            ResetPending();
            return null;
        }
        if (observed == OperationalState)
        {
            ResetPending();
            return null;
        }
        PendingSampleCount = PendingOperationalState == observed ? checked(PendingSampleCount + 1) : 1;
        PendingOperationalState = observed;
        if (PendingSampleCount < debounceSamples) return null;
        var previous = OperationalState;
        OperationalState = observed;
        ResetPending();
        LastTransitionAt = sourceAt.ToUniversalTime();
        return previous;
    }

    private void ResetPending()
    {
        PendingOperationalState = null;
        PendingSampleCount = 0;
    }
}
