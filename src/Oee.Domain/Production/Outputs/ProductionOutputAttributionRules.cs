namespace Oee.Domain.Production;

/// <summary>Không gán cả counter của khoảng gián đoạn dài qua ranh giới kế hoạch vào kế hoạch mới.</summary>
public static class ProductionOutputAttributionRules
{
    public const string CollectionGapReason = "CROSS_PLAN_COLLECTION_GAP";
    public static readonly TimeSpan MaximumObservationGap = TimeSpan.FromMinutes(2);

    public static bool IsUncertainAfterCollectionGap(
        DateTimeOffset? previousObservedAt,
        DateTimeOffset receivedAt,
        DateTimeOffset producedAt,
        DateTimeOffset planStart)
        => previousObservedAt.HasValue
            && receivedAt - previousObservedAt.Value > MaximumObservationGap
            && previousObservedAt.Value < planStart
            && producedAt > planStart;
}
