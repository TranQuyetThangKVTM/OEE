namespace Oee.Application.Contracts.Operations;

public sealed record EffectiveStandardRequest(
    Guid WorkUnitId,
    Guid ProductId,
    decimal SpeedPerMinute,
    string Uom,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
