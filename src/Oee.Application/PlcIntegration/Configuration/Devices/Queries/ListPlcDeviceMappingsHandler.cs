using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Queries;

/// <summary>Điều phối thao tác ListPlcDeviceMappings trong cùng scope lưu dữ liệu.</summary>
internal sealed class ListPlcDeviceMappingsHandler(IPlcConfigurationReadStore reads)
    : IRequestHandler<ListPlcDeviceMappingsQuery, PagedResult<PlcDeviceMappingItem>>
{
    public Task<PagedResult<PlcDeviceMappingItem>> HandleAsync(
        ListPlcDeviceMappingsQuery command,
        CancellationToken cancellationToken)
    {
        var query = command.Query;

        return reads.DeviceMappingsAsync(query, cancellationToken);
    }
}
