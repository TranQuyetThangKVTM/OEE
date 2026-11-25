using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeletePlcDeviceConfiguration.</summary>
public sealed record DeletePlcDeviceConfigurationCommand(
    Guid Id,
    string RowVersion) : ICommand;
