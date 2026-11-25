using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Oee.Domain.PlcIntegration;

/// <summary>Bằng chứng đọc lại và hàng rào chống cộng lại counter cũ sau khi tự phục hồi.</summary>
internal sealed record CounterRecoveryStateValue(
    bool Pending,
    decimal ReferenceTotal,
    decimal ReferenceDefect,
    decimal Rate = 0,
    long StartedTicks = 0,
    long ObservedTicks = 0,
    long SourceTicks = 0,
    decimal Total = 0,
    decimal Defect = 0,
    int Increases = 0,
    string Digest = "")
{
    internal const string PendingPrefix = "!cr1|p|";
    private const string ActivePrefix = "!cr1|a|";
    internal bool Confirmed => Pending && Increases >= 2
        && ObservedTicks - StartedTicks >= TimeSpan.FromSeconds(30).Ticks;

    internal CounterRecoveryStateValue Restart(CounterSampleValue sample, DateTimeOffset receivedAt) => this with
    {
        Pending = true, Rate = 0, StartedTicks = receivedAt.UtcTicks, ObservedTicks = receivedAt.UtcTicks,
        SourceTicks = sample.SourceTimestamp.UtcTicks, Total = sample.Total, Defect = sample.Defect,
        Increases = 0, Digest = ""
    };

    internal CounterRecoveryStateValue Observe(CounterSampleValue sample, DateTimeOffset receivedAt)
    {
        if (StartedTicks == 0 || receivedAt.UtcTicks - ObservedTicks > TimeSpan.FromMinutes(10).Ticks
            || sample.Total < Total || sample.Defect < Defect || sample.SourceTimestamp.UtcTicks < SourceTicks)
            return Restart(sample, receivedAt);
        if (receivedAt.UtcTicks <= ObservedTicks || sample.SourceTimestamp.UtcTicks <= SourceTicks)
            return this;

        var step = sample.Total - Total;
        var rate = step / ((receivedAt.UtcTicks - ObservedTicks) / (decimal)TimeSpan.TicksPerSecond);
        // Không dùng một bước gần quay lại mốc cao cũ để suy ra tốc độ của dãy thấp.
        // Các máy chạy theo đợt vẫn được xác nhận; không đòi tốc độ từng lần đọc giống nhau.
        if (sample.Total < ReferenceTotal && step > Math.Max(1, (ReferenceTotal - Total) / 2))
            return Restart(sample, receivedAt);

        return this with
        {
            ObservedTicks = receivedAt.UtcTicks, SourceTicks = sample.SourceTimestamp.UtcTicks,
            Total = sample.Total, Defect = sample.Defect,
            Increases = Math.Min(2, Increases + (step > 0 ? 1 : 0)),
            Rate = Math.Round(Math.Max(Rate, rate), 6)
        };
    }

    internal bool IsUnverifiedJump(CounterStateValue previous, CounterDeltaResultValue delta, double secondsSinceCounterChange)
    {
        if (!delta.Accepted)
            return false;
        // Pending sau khi đã lập mốc thấp vẫn phải giữ hàng rào. Nếu bỏ qua ở đây,
        // mẫu nhảy lớn thứ hai sẽ được cộng trước khi đủ bằng chứng đọc lại.
        if (Pending && ReferenceTotal <= (previous.LastTotal ?? 0) && ReferenceDefect <= (previous.LastDefect ?? 0))
            return false;
        var seconds = Math.Clamp(secondsSinceCounterChange, 1, 30);
        var budget = Math.Max(1, Rate * (decimal)seconds * 4);
        return delta.TotalDelta > budget || delta.DefectDelta > budget;
    }

    internal CounterRecoveryStateValue? Accepted(CounterDeltaResultValue delta, string sampleKey)
    {
        var totalReference = delta.State.LastTotal >= ReferenceTotal ? 0 : ReferenceTotal;
        var defectReference = delta.State.LastDefect >= ReferenceDefect ? 0 : ReferenceDefect;
        if (totalReference == 0 && defectReference == 0) return null;
        return new(false, totalReference, defectReference, Rate, Digest: Hash(sampleKey));
    }

    internal bool Matches(string sampleKey) => !Pending && Digest == Hash(sampleKey);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    // LastSampleKey đã có marker gián đoạn. Encoding gọn giữ cả bằng chứng/guard qua restart
    // trong cột hiện hữu 200 ký tự, không cần thay schema database máy thật.
    internal string Encode() => FormattableString.Invariant(
        $"{(Pending ? PendingPrefix : ActivePrefix)}{ReferenceTotal:G29}|{ReferenceDefect:G29}|{Rate:G29}|{StartedTicks:X}|{ObservedTicks:X}|{SourceTicks:X}|{Total:G29}|{Defect:G29}|{Increases}|{Digest}");

    internal static CounterRecoveryStateValue? Decode(string? value)
    {
        if (value is null || (!value.StartsWith(PendingPrefix, StringComparison.Ordinal) && !value.StartsWith(ActivePrefix, StringComparison.Ordinal))) return null;
        var parts = value.Split('|');
        if (parts.Length != 12
            || !decimal.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var referenceTotal)
            || !decimal.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var referenceDefect)
            || !decimal.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)
            || !long.TryParse(parts[5], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var started)
            || !long.TryParse(parts[6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var observed)
            || !long.TryParse(parts[7], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var source)
            || !decimal.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var total)
            || !decimal.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var defect)
            || !int.TryParse(parts[10], NumberStyles.None, CultureInfo.InvariantCulture, out var increases)) return null;
        return new(parts[1] == "p", referenceTotal, referenceDefect, rate, started, observed, source, total, defect, increases, parts[11]);
    }
}
