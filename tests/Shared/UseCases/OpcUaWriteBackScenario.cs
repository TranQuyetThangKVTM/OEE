using Oee.Application.Common;
using Oee.Application.PlcIntegration;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class OpcUaWriteBackScenario : IOpcUaWriteBackScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public OpcUaWriteBackScenario(IOpcUaWriteBackStore store, IOpcUaClientAdapter adapter, IOpcUaWriteBackSettings settings, IClock clock)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IOpcUaWriteBackStore)] = store, [typeof(IOpcUaClientAdapter)] = adapter, [typeof(IOpcUaWriteBackSettings)] = settings, [typeof(IClock)] = clock });
    }

    public Task WriteAsync(CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.WriteBack.Write.WriteOpcUaValuesCommand(), ct);
}
