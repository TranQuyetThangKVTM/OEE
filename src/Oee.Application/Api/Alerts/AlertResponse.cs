namespace Oee.Application.Contracts.Api;

/// <summary>DTO cảnh báo cho API/mobile; các ratio Actual/Target dùng thang 0..1.</summary>
public sealed record AlertResponse(
    Guid Id,
    string WorkUnit,
    string Metric,
    string Severity,
    string State,
    decimal Actual,
    decimal Target,
    DateTimeOffset TriggeredAt,
    string ConcurrencyToken);
