using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Tests.UseCases;
/// <summary>
/// Use case quản trị connection, device/tag mapping và fault mapping OPC UA.
/// </summary>
/// <remarks>
/// UI làm việc với DTO trung lập, không nhận type của OPC UA SDK và không đọc secret. Implementation
/// kiểm tra capability PlcManage, concurrency, unique key và ghi audit trước khi lưu SQL.
/// </remarks>
public interface IPlcConfigurationScenario
{
    /// <summary>Đọc lookup active cho dialog cấu hình PLC.</summary>
    Task<PlcConfigurationLookups> LookupsAsync(CancellationToken cancellationToken);
    /// <summary>Đọc tổng quan connectivity/health, tách khỏi operational state.</summary>
    // Connection là cấu hình endpoint/security ít thay đổi, không chứa runtime checkpoint.
    Task<PagedResult<OpcUaConnectionItem>> ConnectionsAsync(PlcListQuery query, CancellationToken cancellationToken);
    Task<Guid> CreateConnectionAsync(OpcUaConnectionRequest request, CancellationToken cancellationToken);
    Task UpdateConnectionAsync(Guid id, OpcUaConnectionRequest request, CancellationToken cancellationToken);
    Task DeleteConnectionAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    Task<OpcUaConnectionTestResult> TestConnectionAsync(Guid id, CancellationToken cancellationToken);
    // Device configuration liên kết WorkUnit với connection và các tag mapping động.
    Task<PagedResult<PlcDeviceMappingItem>> DeviceMappingsAsync(PlcListQuery query, CancellationToken cancellationToken);
    Task<PlcDeviceConfiguration> DeviceConfigurationAsync(Guid id, CancellationToken cancellationToken);
    Task<Guid> CreateDeviceConfigurationAsync(PlcDeviceConfigurationRequest request, CancellationToken cancellationToken);
    Task UpdateDeviceConfigurationAsync(Guid id, PlcDeviceConfigurationRequest request, CancellationToken cancellationToken);
    Task DeleteDeviceConfigurationAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    /// <summary>Browse cây NodeId qua adapter; credential được resolve server-side.</summary>
    Task<IReadOnlyCollection<OpcUaBrowseNode>> BrowseAsync(OpcUaBrowseRequest request, CancellationToken cancellationToken);
    // Fault mapping ưu tiên scope WorkUnit trước global; danh sách lớn được paging tại server.
    Task<PagedResult<PlcFaultMappingItem>> FaultMappingsAsync(PlcListQuery query, CancellationToken cancellationToken);
    Task<Guid> CreateFaultMappingAsync(PlcFaultMappingRequest request, CancellationToken cancellationToken);
    Task UpdateFaultMappingAsync(Guid id, PlcFaultMappingRequest request, CancellationToken cancellationToken);
    Task DeleteFaultMappingAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
}
