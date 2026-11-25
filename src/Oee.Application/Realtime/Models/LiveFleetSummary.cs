namespace Oee.Application.Contracts.Modules;

public sealed record LiveFleetSummary(
    int Total,
    int Running,
    int Stopped,
    int Connected,
    int Unhealthy,
    decimal? Oee,
    decimal? Availability,
    decimal? Performance,
    decimal? Quality,
    decimal TotalQuantity,
    decimal GoodQuantity,
    decimal RejectQuantity,
    decimal TargetQuantity,
    int OpenAlerts);
