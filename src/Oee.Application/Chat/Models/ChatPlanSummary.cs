using Oee.Application.Contracts.Modules;

namespace Oee.Application.Chat;

public sealed record ChatPlanSummary(
    int TotalCount,
    decimal Planned,
    decimal Actual,
    IReadOnlyCollection<ProductionPlanListItem> Items);
