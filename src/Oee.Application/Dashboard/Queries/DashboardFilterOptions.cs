namespace Oee.Application.Contracts.Modules;

public sealed record DashboardFilterOptions(
    DateOnly Today,
    IReadOnlyCollection<DashboardFilterOption> Sites,
    IReadOnlyCollection<DashboardFilterOption> Areas,
    IReadOnlyCollection<DashboardFilterOption> WorkCenters,
    IReadOnlyCollection<DashboardFilterOption> WorkUnits,
    IReadOnlyCollection<DashboardFilterOption> Products,
    IReadOnlyCollection<DashboardFilterOption> Shifts);
