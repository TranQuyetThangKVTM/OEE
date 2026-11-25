using Oee.Domain.Common;

namespace Oee.Domain.PlcIntegration;

/// <summary>
/// Pure service tính delta counter có xét initial sync, trùng/out-of-order, reset và rollover.
/// </summary>
/// <remarks>
/// Calculator không ghi SQL. Worker/processor chịu trách nhiệm persist checkpoint và dùng SampleKey
/// cho idempotency. Initial sync trả delta 0 để không biến toàn bộ counter tích lũy thành sản lượng mới.
/// </remarks>
public static class CounterDeltaCalculator
{
    /// <summary>Tính phần tăng giữa checkpoint và sample mới.</summary>
    /// <param name="checkpoint">Trạng thái đã xử lý/persist trước đó.</param>
    /// <param name="sample">Mẫu counter mới cùng source timestamp.</param>
    /// <param name="mode">Quy ước counter tích lũy hay reset theo ca/ngày.</param>
    /// <param name="rolloverMaximum">Giá trị tối đa nếu counter phần cứng có rollover.</param>
    /// <param name="isReconnect">Lượt đọc đầu của session mới không được xác nhận một reset counter.</param>
    /// <returns>Delta và checkpoint mới; Accepted=false khi mẫu trùng hoặc đến sai thứ tự.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Khi PLC trả counter âm.</exception>
    public static CounterDeltaResultValue Calculate(CounterStateValue checkpoint, CounterSampleValue sample, CounterModeEnum mode, decimal? rolloverMaximum = null, bool isReconnect = false)
    {
        // Mẫu cũ không được cộng lại; timestamp của frame có thể đứng khi tag khác đang đi trước.
        if (sample.Total < 0 || sample.Defect < 0) throw new ArgumentOutOfRangeException(nameof(sample));
        // Startup defaults có thể mang timestamp cũ. Vẫn báo mất liên tục counter để các
        // lượt refresh sau không coi 0 là reset theo ca/ngày vừa được xác nhận.
        if (isReconnect && checkpoint.LastTotal.HasValue
            && (sample.Total < checkpoint.LastTotal || sample.Defect < (checkpoint.LastDefect ?? 0)))
            return new(false, 0, 0, checkpoint, "ReconnectCounterDecrease");
        if (checkpoint.LastTimestamp.HasValue)
        {
            // Frame dùng timestamp lớn nhất của Run/Total/Defect. Total vẫn có thể tăng
            // trong khi timestamp lớn nhất chưa đổi. Chỉ nhận cùng timestamp khi cả hai
            // counter không giảm và ít nhất một counter tăng; replay vẫn có delta bằng 0.
            var monotonicIncrease = checkpoint.LastTotal.HasValue
                && sample.Total >= checkpoint.LastTotal.Value
                && sample.Defect >= (checkpoint.LastDefect ?? 0)
                && (sample.Total > checkpoint.LastTotal.Value || sample.Defect > (checkpoint.LastDefect ?? 0));
            if (sample.SourceTimestamp < checkpoint.LastTimestamp
                || (sample.SourceTimestamp == checkpoint.LastTimestamp && !monotonicIncrease))
                return new(false, 0, 0, checkpoint, "OutOfOrderOrDuplicate");
        }
        // Lần đầu chỉ đồng bộ mốc. Delta 0 tránh double insert sau lần khởi động đầu tiên.
        if (!checkpoint.LastTotal.HasValue)
            return new(true, 0, 0, new(sample.Total, sample.Defect, sample.SourceTimestamp, checkpoint.Epoch), "InitialSync");
        // Counter giảm có thể là reset có chủ đích hoặc rollover của thanh ghi hữu hạn.
        var totalReset = sample.Total < checkpoint.LastTotal;
        var defectReset = sample.Defect < (checkpoint.LastDefect ?? 0);
        var reset = totalReset || defectReset;
        // OPC server có thể tạm trả 0 rồi khôi phục counter cũ. Không hạ checkpoint của
        // counter tích lũy khi chưa có bằng chứng reset; nếu hạ, lần khôi phục bị cộng lại toàn bộ.
        // Không suy luận reset vật lý chỉ từ một counter giảm; lưu mapping không được hạ mốc.
        var unconfirmedReset = (totalReset && !IsRollover(checkpoint.LastTotal.Value, sample.Total, rolloverMaximum))
            || (defectReset && !IsRollover(checkpoint.LastDefect ?? 0, sample.Defect, rolloverMaximum));
        if (mode == CounterModeEnum.Cumulative && unconfirmedReset)
            return new(false, 0, 0, checkpoint, "UnexpectedCounterDecrease");
        var totalDelta = Delta(checkpoint.LastTotal.Value, sample.Total, totalReset, rolloverMaximum);
        var defectDelta = Delta(checkpoint.LastDefect ?? 0, sample.Defect, defectReset, rolloverMaximum);
        var epoch = checkpoint.Epoch + (reset ? 1 : 0);
        // Reason giúp log/metric phân biệt reset bất thường với reset đúng chế độ cấu hình.
        var reason = reset ? (mode == CounterModeEnum.Cumulative ? "ResetOrRollover" : "ExpectedReset") : null;
        return new(true, totalDelta, defectDelta, new(sample.Total, sample.Defect, sample.SourceTimestamp, epoch), reason);
    }

    private static bool IsRollover(decimal previous, decimal current, decimal? maximum)
        => maximum.HasValue && previous <= maximum.Value && current <= maximum.Value && previous > maximum.Value * 0.8m;

    // Chỉ gọi sau khi chính sách reset/rollover đã chấp nhận mẫu.
    private static decimal Delta(decimal previous, decimal current, bool reset, decimal? maximum) => !reset
        ? current - previous
        : IsRollover(previous, current, maximum)
            ? maximum!.Value - previous + current + 1
            : current;
}
