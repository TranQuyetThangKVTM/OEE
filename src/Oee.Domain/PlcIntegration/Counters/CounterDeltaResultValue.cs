namespace Oee.Domain.PlcIntegration;

/// <summary>Kết quả chấp nhận/từ chối sample và phần tăng được phép ghi tiếp.</summary>
public sealed record CounterDeltaResultValue(bool Accepted, decimal TotalDelta, decimal DefectDelta, CounterStateValue State, string? Reason);
