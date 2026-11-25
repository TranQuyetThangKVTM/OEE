using Oee.Domain.Common;

namespace Oee.Domain.PlcIntegration;

/// <summary>Mốc counter cuối đã xử lý, cho phép worker restart/replay mà không double insert output.</summary>
public sealed class CounterCheckpointEntity : Entity
{
    private const string DiscontinuityMarker = "!counter-discontinuity";

    public Guid PlcDeviceMappingId { get; private set; }
    public PlcDeviceMappingEntity PlcDeviceMapping { get; private set; } = null!;
    public decimal? LastTotalCount { get; private set; }
    public decimal? LastDefectCount { get; private set; }
    public DateTimeOffset? LastReadAt { get; private set; }
    /// <summary>Lần cuối counter đổi hoặc ghi nhận delta dương; đọc lại giá trị cũ không đổi mốc này.</summary>
    public DateTimeOffset? LastCounterChangedAt { get; private set; }
    public long CounterEpoch { get; private set; }
    public string? LastSampleKey { get; private set; }
    private byte[] rowVersion = [];
    public byte[] RowVersion
    {
        get => rowVersion.ToArray();
        private set => rowVersion = value.ToArray();
    }

    public const string RecoveryPendingPrefix = "!cr1|p|";
    public bool IsDiscontinuityPending => LastSampleKey == DiscontinuityMarker
        || LastSampleKey?.StartsWith(RecoveryPendingPrefix, StringComparison.Ordinal) == true;
    public CounterStateValue State => new(LastTotalCount, LastDefectCount, LastReadAt, CounterEpoch);

    private CounterCheckpointEntity()
    {
    }

    public static CounterCheckpointEntity Create(Guid plcDeviceMappingId)
    {
        return new CounterCheckpointEntity
        {
            PlcDeviceMappingId = DomainGuard.RequiredId(plcDeviceMappingId)
        };
    }

    public bool IsDuplicateSample(string sampleKey) => LastSampleKey == sampleKey
        || CounterRecoveryStateValue.Decode(LastSampleKey)?.Matches(sampleKey) == true;

    public bool RequiresRecoveryRead(CounterDeltaResultValue delta, DateTimeOffset observedAt) =>
        CounterRecoveryStateValue.Decode(LastSampleKey)?.IsUnverifiedJump(
            State, delta, (observedAt - (LastCounterChangedAt ?? LastReadAt ?? observedAt)).TotalSeconds) == true;

    public void MarkDiscontinuity()
    {
        if (IsDiscontinuityPending) return;
        var recovery = CounterRecoveryStateValue.Decode(LastSampleKey);
        LastSampleKey = recovery is null ? DiscontinuityMarker
            : new CounterRecoveryStateValue(true, recovery.ReferenceTotal, recovery.ReferenceDefect).Encode();
    }

    /// <summary>Chỉ đọc trực tiếp, Good, timestamp mới và tăng ổn định mới được lập mốc mới (delta 0).</summary>
    public CounterDeltaResultValue? ObserveRecovery(CounterSampleValue sample, DateTimeOffset receivedAt, bool readRefresh, bool reconnect)
    {
        if (!IsDiscontinuityPending || !LastTotalCount.HasValue) return null;
        var recovery = CounterRecoveryStateValue.Decode(LastSampleKey)
            ?? new(true, LastTotalCount.Value, LastDefectCount ?? 0);
        if (reconnect || sample.Total <= 0 || sample.Defect < 0
            || sample.SourceTimestamp > receivedAt.AddSeconds(15) || sample.SourceTimestamp < receivedAt.AddMinutes(-2))
        {
            LastSampleKey = new CounterRecoveryStateValue(true, recovery.ReferenceTotal, recovery.ReferenceDefect).Encode();
            return null;
        }
        if (!readRefresh) return null;
        recovery = recovery.Observe(sample, receivedAt);
        LastSampleKey = recovery.Encode();
        return recovery.Confirmed
            ? new(true, 0, 0, new(sample.Total, sample.Defect, sample.SourceTimestamp, checked(CounterEpoch + 1)), "CounterRecovered")
            : null;
    }

    public void Apply(CounterDeltaResultValue delta, string sampleKey, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (!delta.Accepted) throw new InvalidOperationException("A rejected counter sample cannot change the checkpoint.");
        if (RequiresRecoveryRead(delta, observedAt)) throw new InvalidOperationException("An unverified recovery jump cannot change the checkpoint.");
        var key = DomainGuard.Required(sampleKey, 200);
        if (delta.State.LastTotal is { } total) DomainGuard.NonNegative(total, nameof(delta.State.LastTotal));
        if (delta.State.LastDefect is { } defect) DomainGuard.NonNegative(defect, nameof(delta.State.LastDefect));
        if (delta.State.Epoch < CounterEpoch) throw new ArgumentOutOfRangeException(nameof(delta));
        if (LastCounterChangedAt is null || delta.TotalDelta > 0 || LastTotalCount != delta.State.LastTotal)
            LastCounterChangedAt = observedAt.ToUniversalTime();
        LastTotalCount = delta.State.LastTotal;
        LastDefectCount = delta.State.LastDefect;
        LastReadAt = delta.State.LastTimestamp?.ToUniversalTime();
        CounterEpoch = delta.State.Epoch;
        LastSampleKey = CounterRecoveryStateValue.Decode(LastSampleKey)?.Accepted(delta, key)?.Encode() ?? key;
    }

    public void Reset()
    {
        var nextEpoch = checked(CounterEpoch + 1);
        LastTotalCount = null;
        LastDefectCount = null;
        LastReadAt = null;
        LastCounterChangedAt = null;
        LastSampleKey = null;
        CounterEpoch = nextEpoch;
    }
}
