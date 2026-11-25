namespace Oee.Application.Operations;

public sealed record SamplePlanLookup(
    Guid Id,
    string Code,
    Guid SiteId);
