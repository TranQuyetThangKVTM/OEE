using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteOpcUaConnection.</summary>
public sealed record DeleteOpcUaConnectionCommand(
    Guid Id,
    string RowVersion) : ICommand;
