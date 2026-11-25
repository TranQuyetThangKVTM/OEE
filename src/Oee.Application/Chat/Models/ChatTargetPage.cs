using Oee.Application.Contracts.Modules;

namespace Oee.Application.Chat;

public sealed record ChatTargetPage(
    IReadOnlyCollection<TargetListItem> Items,
    int TotalCount);
