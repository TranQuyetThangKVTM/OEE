namespace Oee.Application.Chat;

public sealed record ChatCatalogRow(
    string Code,
    string Name,
    string? Detail = null,
    decimal? Quantity = null,
    DateTimeOffset? At = null);
