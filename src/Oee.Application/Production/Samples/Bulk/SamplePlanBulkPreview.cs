namespace Oee.Application.Contracts.Operations;

/// <summary>Preview thêm dòng mẫu, gồm tối đa, số mới và số duplicate.</summary>
public sealed record SamplePlanBulkPreview(
    int MaximumCount,
    int NewCount,
    int DuplicateCount,
    IReadOnlyCollection<string> DuplicateReasons);
