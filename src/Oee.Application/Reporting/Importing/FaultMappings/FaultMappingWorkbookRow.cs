namespace Oee.Application.Reporting;

public sealed record FaultMappingWorkbookRow(
    int RowNumber,
    string WorkUnitCode,
    string FaultCode,
    string ReasonCode,
    string Description,
    string IsActiveText);
