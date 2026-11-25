namespace Oee.Application.Contracts.Modules;

/// <summary>Gói lookup Active cho các form Production, tải bằng Application query thay vì hard-code UI.</summary>
public sealed record ProductionLookups(
    IReadOnlyCollection<ProductionLookupOption> WorkUnits,
    IReadOnlyCollection<ProductionLookupOption> WorkCenters,
    IReadOnlyCollection<ProductionLookupOption> Products,
    IReadOnlyCollection<ProductionLookupOption> Shifts,
    IReadOnlyCollection<ProductionLookupOption> DowntimeReasons);
