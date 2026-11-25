namespace Oee.Application.Chat;

public sealed record ChatCatalogPage(
    IReadOnlyCollection<ChatCatalogRow> Items,
    int TotalCount,
    int? TotalEvents = null);
