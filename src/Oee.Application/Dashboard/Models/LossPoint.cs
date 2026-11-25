namespace Oee.Application.Contracts.Modules;

public sealed record LossPoint(
    string ReasonCode,
    string Reason,
    string Category,
    int EventCount,
    decimal Minutes,
    decimal Percentage);
