namespace Oee.Application.Contracts.Reporting;

public sealed record FaultMappingImportRow(
    int RowNumber,
    string? WorkUnitCode,
    string FaultCode,
    string ReasonCode,
    string? Description,
    bool IsActive);
