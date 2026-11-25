using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdatePlcFaultMapping.</summary>
public sealed record UpdatePlcFaultMappingCommand(
    Guid Id,
    PlcFaultMappingRequest Request) : ICommand;
