namespace Oee.Application.PlcIntegration;

/// <summary>Kết quả processor gồm delta được chấp nhận và trạng thái quarantine.</summary>
public sealed record PlcProcessingResult(
    bool Accepted,
    decimal TotalDelta,
    decimal DefectDelta,
    bool Quarantined,
    string? Reason);
