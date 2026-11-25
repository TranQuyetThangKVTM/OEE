using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeletePlcFaultMapping.</summary>
public sealed record DeletePlcFaultMappingCommand(
    Guid Id,
    string RowVersion) : ICommand;
