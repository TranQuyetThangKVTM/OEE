using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác BrowseOpcUaNodes.</summary>
public sealed record BrowseOpcUaNodesQuery(OpcUaBrowseRequest Request)
    : IQuery<IReadOnlyCollection<OpcUaBrowseNode>>;
