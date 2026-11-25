namespace Oee.Application.Contracts.Modules;

public sealed record EndTargetRequest(
    DateOnly EffectiveTo,
    string RowVersion);
