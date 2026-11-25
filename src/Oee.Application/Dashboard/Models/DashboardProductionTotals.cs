namespace Oee.Application.Contracts.Modules;

/// <summary>Tổng sản lượng Total/Good/Reject/Target theo filter.</summary>
public sealed record DashboardProductionTotals(
    decimal Total,
    decimal Good,
    decimal Reject,
    decimal Target);
