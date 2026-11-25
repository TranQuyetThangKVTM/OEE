using Oee.Application.Common;

namespace Oee.Application.Reporting.Importing.FaultMappings.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác FaultMappingImport.</summary>
internal sealed class FaultMappingImportContext(
    IUseCaseExecutor requests,
    IFaultMappingWorkbook workbook,
    IFaultMappingImportStore store,
    IApplicationUnitOfWork unitOfWork)
    : IUseCaseContext
{
    internal IUseCaseExecutor Requests => requests;
    internal IFaultMappingWorkbook Workbook => workbook;
    internal IFaultMappingImportStore Store => store;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;

    internal const string Version = FaultMappingWorkbookData.TemplateVersion;

    internal static string Clean(string value) => value.Trim();
}
