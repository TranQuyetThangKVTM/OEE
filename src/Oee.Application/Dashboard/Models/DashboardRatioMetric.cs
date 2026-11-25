namespace Oee.Application.Contracts.Modules;

/// <summary>Một KPI ratio 0..1 gồm actual, target, kỳ trước và độ thay đổi.</summary>
public sealed record DashboardRatioMetric(
    decimal? Value,
    decimal? Target,
    decimal? PreviousValue,
    decimal? Change);
