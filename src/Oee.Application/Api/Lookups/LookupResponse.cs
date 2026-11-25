namespace Oee.Application.Contracts.Api;

/// <summary>DTO lookup gọn cho dropdown/autocomplete của client.</summary>
public sealed record LookupResponse(
    Guid Id,
    string Code,
    string Name,
    string Detail,
    string Status);
