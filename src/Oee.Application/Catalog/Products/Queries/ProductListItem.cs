namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng bảng Product, có RowVersion base64 cho optimistic concurrency.</summary>
public sealed record ProductListItem(
    Guid Id,
    string Code,
    string Name,
    string DefaultUom,
    string? Description,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion);
