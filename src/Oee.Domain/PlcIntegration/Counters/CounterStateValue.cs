namespace Oee.Domain.PlcIntegration;

/// <summary>Checkpoint counter gần nhất đã persist, dùng tiếp sau khi Worker restart.</summary>
/// <param name="LastTotal">Total counter lần cuối; null nghĩa là chưa initial sync.</param>
/// <param name="LastDefect">Defect counter lần cuối.</param>
/// <param name="LastTimestamp">Source timestamp cuối đã nhận từ PLC.</param>
/// <param name="Epoch">Số lần counter đã reset/rollover được nhận diện.</param>
public sealed record CounterStateValue(decimal? LastTotal, decimal? LastDefect, DateTimeOffset? LastTimestamp, long Epoch);
