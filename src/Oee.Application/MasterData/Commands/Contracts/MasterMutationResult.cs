namespace Oee.Application.Contracts.Operations;

/// <summary>Kết quả mutation master; RowVersion mới phải được UI dùng cho lần cập nhật kế tiếp.</summary>
public sealed record MasterMutationResult(
    Guid Id,
    string RowVersion);
