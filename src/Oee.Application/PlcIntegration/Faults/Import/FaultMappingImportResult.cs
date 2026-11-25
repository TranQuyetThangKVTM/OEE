namespace Oee.Application.Contracts.Reporting;

/// <summary>Kết quả import typed giúp đối chiếu inserted/updated/skipped và lỗi theo dòng.</summary>
public sealed record FaultMappingImportResult(
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyCollection<FaultMappingImportIssue> Issues);
