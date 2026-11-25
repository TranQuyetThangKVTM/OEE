using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdatePlcDeviceConfiguration.</summary>
public sealed record UpdatePlcDeviceConfigurationCommand(
    Guid Id,
    PlcDeviceConfigurationRequest Request)
    : ICommand;
