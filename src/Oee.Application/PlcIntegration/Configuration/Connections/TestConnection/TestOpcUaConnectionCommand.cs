using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.TestConnection;

/// <summary>Dữ liệu yêu cầu cho thao tác TestOpcUaConnection.</summary>
public sealed record TestOpcUaConnectionCommand(Guid Id) : ICommand<OpcUaConnectionTestResult>;
