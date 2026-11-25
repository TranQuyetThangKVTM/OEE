using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreatePlcFaultMapping.</summary>
public sealed record CreatePlcFaultMappingCommand(PlcFaultMappingRequest Request) : ICommand<Guid>;
