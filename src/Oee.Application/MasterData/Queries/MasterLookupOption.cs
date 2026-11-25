namespace Oee.Application.Contracts.Modules;

/// <summary>DTO lookup master cho MudAutocomplete/MudSelect; Active giúp UI chỉ cho chọn bản ghi hợp lệ.</summary>
public sealed record MasterLookupOption(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId = null,
    string? ParentName = null,
    bool Active = true);
