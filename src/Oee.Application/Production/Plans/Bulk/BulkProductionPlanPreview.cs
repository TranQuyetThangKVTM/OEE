namespace Oee.Application.Contracts.Operations;

/// <summary>Preview: Requested là số tổ hợp đầu vào; Valid có thể tạo; Skipped trùng; Conflict xung đột SKU/kỳ.</summary>
public sealed record BulkProductionPlanPreview(
    int RequestedCount,
    int ValidCount,
    int SkippedCount,
    int ConflictCount,
    IReadOnlyCollection<BulkPlanCombination> Combinations);
