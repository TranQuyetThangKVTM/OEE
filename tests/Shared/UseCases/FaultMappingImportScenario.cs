using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class FaultMappingImportScenario : IFaultMappingImportScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public FaultMappingImportScenario(IFaultMappingWorkbook workbook, IFaultMappingImportStore store, IApplicationUnitOfWork unitOfWork)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IFaultMappingWorkbook)] = workbook, [typeof(IFaultMappingImportStore)] = store, [typeof(IApplicationUnitOfWork)] = unitOfWork });
    }

    public byte[] CreateTemplate() => this.requests.ExecuteAsync(new global::Oee.Application.Reporting.Importing.FaultMappings.Queries.CreateTemplateFaultMappingsQuery(), default).GetAwaiter().GetResult();
    public Task<FaultMappingImportPreview> PreviewAsync(Stream xlsx, long length, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Reporting.Importing.FaultMappings.Preview.PreviewFaultMappingsQuery(xlsx, length), ct);
    public Task<FaultMappingImportResult> ImportAsync(Stream xlsx, long length, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Reporting.Importing.FaultMappings.Import.ImportFaultMappingsCommand(xlsx, length), ct);
}
