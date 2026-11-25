using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Queries;

/// <summary>Điều phối thao tác BrowseOpcUaNodes trong cùng scope lưu dữ liệu.</summary>
internal sealed class BrowseOpcUaNodesHandler(PlcConnectionContext context)
    : IRequestHandler<BrowseOpcUaNodesQuery, IReadOnlyCollection<OpcUaBrowseNode>>
{
    public async Task<IReadOnlyCollection<OpcUaBrowseNode>> HandleAsync(
        BrowseOpcUaNodesQuery command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var connection = await context.store.FindConnectionAsync(request.ConnectionId, false, cancellationToken)
            ?? throw Missing("OPC UA connection");

        try
        {
            await using var session = await context.opcUa.ConnectAsync(PlcConnectionContext.ToEndpoint(connection), cancellationToken);
            var nodes = await session.BrowseAsync(
                string.IsNullOrWhiteSpace(request.ParentNodeId) ? null : request.ParentNodeId.Trim(),
                cancellationToken);

            return nodes.Select(x => new OpcUaBrowseNode(x.NodeId, x.DisplayName, x.DataType, x.HasChildren, x.IsVariable)).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException("Không thể browse OPC UA. Kiểm tra kết nối và security; chi tiết nhạy cảm đã được ẩn.");
        }
    }
}
