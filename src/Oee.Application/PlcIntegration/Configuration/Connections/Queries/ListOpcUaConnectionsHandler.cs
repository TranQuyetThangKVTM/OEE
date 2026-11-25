using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Queries;

/// <summary>Điều phối thao tác ListOpcUaConnections trong cùng scope lưu dữ liệu.</summary>
internal sealed class ListOpcUaConnectionsHandler(IPlcConfigurationReadStore reads)
    : IRequestHandler<ListOpcUaConnectionsQuery, PagedResult<OpcUaConnectionItem>>
{
    public Task<PagedResult<OpcUaConnectionItem>> HandleAsync(
        ListOpcUaConnectionsQuery command,
        CancellationToken cancellationToken)
    {
        var query = command.Query;

        return reads.ConnectionsAsync(query, cancellationToken);
    }
}
