namespace Oee.Application.Contracts.Reporting;

public sealed record FaultMappingImportIssue(
    int RowNumber,
    string Field,
    string Code,
    string Message);
