namespace Oee.Application.Contracts.Audit;

/// <summary>Kết quả typed của retention để đối chiếu candidate/deleted và thời lượng chạy.</summary>
public sealed record AuditRetentionResult(
    DateTimeOffset CutoffUtc,
    int CandidateCount,
    int DeletedCount,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);
