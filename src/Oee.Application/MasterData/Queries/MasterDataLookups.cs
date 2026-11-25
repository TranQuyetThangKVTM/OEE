namespace Oee.Application.Contracts.Modules;

/// <summary>Gói lookup dùng chung cho dialog master, tránh N+1 và danh sách hard-code trong component.</summary>
public sealed record MasterDataLookups(
    IReadOnlyCollection<MasterLookupOption> Enterprises,
    IReadOnlyCollection<MasterLookupOption> Sites,
    IReadOnlyCollection<MasterLookupOption> Areas,
    IReadOnlyCollection<MasterLookupOption> WorkCenters,
    IReadOnlyCollection<MasterLookupOption> WorkUnits,
    IReadOnlyCollection<MasterLookupOption> Products,
    IReadOnlyCollection<MasterLookupOption> Statuses,
    IReadOnlyCollection<MasterLookupOption> DowntimeCategories,
    IReadOnlyCollection<MasterLookupOption> DowntimeKinds,
    IReadOnlyCollection<string> TimeZoneIds);
