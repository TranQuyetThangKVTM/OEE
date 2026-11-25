using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác ListOpcUaConnections.</summary>
public sealed record ListOpcUaConnectionsQuery(PlcListQuery Query) : IQuery<PagedResult<OpcUaConnectionItem>>;
