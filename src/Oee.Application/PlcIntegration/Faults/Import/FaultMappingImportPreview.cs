namespace Oee.Application.Contracts.Reporting;

/// <summary>Kết quả dry-run: chỉ ValidRows được phép đi tới transaction import.</summary>
public sealed record FaultMappingImportPreview(
    string TemplateVersion,
    int TotalRows,
    IReadOnlyCollection<FaultMappingImportRow> ValidRows,
    IReadOnlyCollection<FaultMappingImportIssue> Issues);
