using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Queries;

/// <summary>Điều phối thao tác ListPlcFaultMappings trong cùng scope lưu dữ liệu.</summary>
internal sealed class ListPlcFaultMappingsHandler(IPlcConfigurationReadStore reads)
    : IRequestHandler<ListPlcFaultMappingsQuery, PagedResult<PlcFaultMappingItem>>
{
    public Task<PagedResult<PlcFaultMappingItem>> HandleAsync(
        ListPlcFaultMappingsQuery command,
        CancellationToken cancellationToken)
    {
        var query = command.Query;

        return reads.FaultMappingsAsync(query, cancellationToken);
    }
}
