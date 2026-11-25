namespace Oee.Application.Contracts.Operations;

/// <summary>Kết quả commit bulk; CreatedCount và ID chỉ phản ánh các row đã transaction thành công.</summary>
public sealed record BulkProductionPlanResult(
    int RequestedCount,
    int CreatedCount,
    int SkippedCount,
    int ConflictCount,
    IReadOnlyCollection<Guid> CreatedPlanIds,
    IReadOnlyCollection<BulkPlanCombination> Combinations);
