using Oee.Application.Common;
using Oee.Application.Operations;
using Oee.Application.PlcIntegration;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class PlcTelemetryScenario : IPlcTelemetryScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public PlcTelemetryScenario(IPlcTelemetryStore store, IProductionOutputStore outputs, IDowntimeStore downtimes, IApplicationUnitOfWork unitOfWork, IClock clock)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IPlcTelemetryStore)] = store, [typeof(IProductionOutputStore)] = outputs, [typeof(IDowntimeStore)] = downtimes, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock });
    }

    public Task<PlcProcessingResult> ProcessAsync(DeviceTelemetryFrame frame, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Processing.Process.ProcessPlcTelemetryCommand(frame), ct);
}
