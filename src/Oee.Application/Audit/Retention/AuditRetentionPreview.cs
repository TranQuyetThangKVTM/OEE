namespace Oee.Application.Contracts.Audit;

/// <summary>Preview cutoff UTC và số ứng viên trước khi người quản trị xác nhận cleanup.</summary>
public sealed record AuditRetentionPreview(
    DateTimeOffset CutoffUtc,
    int CandidateCount,
    int RetentionMonths);
