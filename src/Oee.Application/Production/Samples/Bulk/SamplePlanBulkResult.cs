namespace Oee.Application.Contracts.Operations;

/// <summary>Kết quả bulk mẫu; Requested/Created/Skipped giúp UI và audit đối chiếu chính xác.</summary>
public sealed record SamplePlanBulkResult(
    Guid SamplePlanId,
    int RequestedCount,
    int CreatedCount,
    int SkippedCount,
    IReadOnlyCollection<SamplePlanItemResponse> CreatedItems,
    IReadOnlyCollection<string> SkippedReasons);
