namespace Oee.Application.Production.Automation;

public sealed record PlanAutomationResult(
    DateOnly PlanDate,
    DateTimeOffset CompletedAt,
    int Created,
    int Skipped,
    int Conflicts,
    int Invalid);
