using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreatePlcDeviceConfiguration.</summary>
public sealed record CreatePlcDeviceConfigurationCommand(PlcDeviceConfigurationRequest Request)
    : ICommand<Guid>;
