using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateOpcUaConnection.</summary>
public sealed record CreateOpcUaConnectionCommand(OpcUaConnectionRequest Request) : ICommand<Guid>;
