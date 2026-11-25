using Oee.Application.Contracts.Reporting;

namespace Oee.Application.Reporting;

public sealed record FaultMappingWorkbookData(
    int TotalRows,
    IReadOnlyCollection<FaultMappingWorkbookRow> Rows,
    IReadOnlyCollection<FaultMappingImportIssue> Issues)
{
    public const string TemplateVersion = "OEE-FAULT-MAPPING-1.0";
}
