namespace Oee.Application.Operations;

internal sealed record SamplePlanCombination(
    Guid WorkUnitId,
    string WorkUnitCode,
    Guid WorkUnitSiteId,
    Guid ShiftId,
    string ShiftCode,
    Guid ShiftSiteId);
