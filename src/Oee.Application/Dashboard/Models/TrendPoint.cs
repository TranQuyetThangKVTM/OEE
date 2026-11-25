namespace Oee.Application.Contracts.Modules;

public sealed record TrendPoint(
    DateOnly Date,
    decimal? Oee,
    decimal? Availability,
    decimal? Performance,
    decimal? Quality);
