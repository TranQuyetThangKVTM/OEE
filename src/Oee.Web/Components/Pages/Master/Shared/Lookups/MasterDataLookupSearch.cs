namespace Oee.Web.Components.Pages.Master;

internal static class MasterDataLookupSearch
{
    internal static Task<IEnumerable<string>> TimeZonesAsync(IEnumerable<string>? source, string? value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var zones = source ?? [];
        if (!string.IsNullOrWhiteSpace(value))
            zones = zones.Where(zone => zone.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(zones.Take(20));
    }
}
