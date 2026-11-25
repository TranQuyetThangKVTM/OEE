using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác GetPlcDeviceConfiguration.</summary>
public sealed record GetPlcDeviceConfigurationQuery(Guid Id) : IQuery<PlcDeviceConfiguration>;
