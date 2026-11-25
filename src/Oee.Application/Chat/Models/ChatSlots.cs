namespace Oee.Application.Chat;

public sealed record ChatSlots(
    ChatTimeRange Range,
    int TopN,
    string? WorkUnitCodeA,
    string? WorkUnitCodeB,
    string? Topic = null,
    string? MasterDataEntity = null);
