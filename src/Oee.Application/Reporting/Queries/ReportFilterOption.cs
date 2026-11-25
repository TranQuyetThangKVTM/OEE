namespace Oee.Application.Contracts.Reporting;

public sealed record ReportFilterOption(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId = null,
    string? Detail = null);
