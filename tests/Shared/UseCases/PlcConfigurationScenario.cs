using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.Operations;
using Oee.Application.PlcIntegration;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class PlcConfigurationScenario : IPlcConfigurationScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public PlcConfigurationScenario(IPlcConfigurationStore store, IMasterDataStore masters, IPlcConfigurationReadStore reads, IApplicationUnitOfWork unitOfWork, IClock clock, IOpcUaClientAdapter opcUa)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IPlcConfigurationStore)] = store, [typeof(IMasterDataStore)] = masters, [typeof(IPlcConfigurationReadStore)] = reads, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock, [typeof(IOpcUaClientAdapter)] = opcUa });
    }

    public Task<PlcConfigurationLookups> LookupsAsync(CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Overview.Queries.GetPlcLookupsQuery(), ct);
    public Task<PagedResult<OpcUaConnectionItem>> ConnectionsAsync(PlcListQuery query, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Connections.Queries.ListOpcUaConnectionsQuery(query), ct);
    public Task<Guid> CreateConnectionAsync(OpcUaConnectionRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Connections.Create.CreateOpcUaConnectionCommand(request), ct);
    public Task UpdateConnectionAsync(Guid id, OpcUaConnectionRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Connections.Update.UpdateOpcUaConnectionCommand(id, request), ct);
    public Task DeleteConnectionAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Connections.Delete.DeleteOpcUaConnectionCommand(id, rowVersion), ct);
    public Task<OpcUaConnectionTestResult> TestConnectionAsync(Guid id, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Connections.TestConnection.TestOpcUaConnectionCommand(id), ct);
    public Task<PagedResult<PlcDeviceMappingItem>> DeviceMappingsAsync(PlcListQuery query, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Devices.Queries.ListPlcDeviceMappingsQuery(query), ct);
    public Task<PlcDeviceConfiguration> DeviceConfigurationAsync(Guid id, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Devices.Queries.GetPlcDeviceConfigurationQuery(id), ct);
    public Task<Guid> CreateDeviceConfigurationAsync(PlcDeviceConfigurationRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Devices.Create.CreatePlcDeviceConfigurationCommand(request), ct);
    public Task UpdateDeviceConfigurationAsync(Guid id, PlcDeviceConfigurationRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Devices.Update.UpdatePlcDeviceConfigurationCommand(id, request), ct);
    public Task DeleteDeviceConfigurationAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Devices.Delete.DeletePlcDeviceConfigurationCommand(id, rowVersion), ct);
    public Task<IReadOnlyCollection<OpcUaBrowseNode>> BrowseAsync(OpcUaBrowseRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Connections.Queries.BrowseOpcUaNodesQuery(request), ct);
    public Task<PagedResult<PlcFaultMappingItem>> FaultMappingsAsync(PlcListQuery query, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Faults.Queries.ListPlcFaultMappingsQuery(query), ct);
    public Task<Guid> CreateFaultMappingAsync(PlcFaultMappingRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Faults.Create.CreatePlcFaultMappingCommand(request), ct);
    public Task UpdateFaultMappingAsync(Guid id, PlcFaultMappingRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Faults.Update.UpdatePlcFaultMappingCommand(id, request), ct);
    public Task DeleteFaultMappingAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.PlcIntegration.Configuration.Faults.Delete.DeletePlcFaultMappingCommand(id, rowVersion), ct);
}
