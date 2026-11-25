using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateOpcUaConnection.</summary>
public sealed record UpdateOpcUaConnectionCommand(
    Guid Id,
    OpcUaConnectionRequest Request) : ICommand;
