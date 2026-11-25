using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

/// <summary>Cấu hình hiển thị, không chứa endpoint hay thông tin xác thực.</summary>
public sealed record OpcTagMonitorMapping(
    Guid Id,
    Guid DeviceId,
    string DeviceCode,
    string DeviceName,
    string ConnectionCode,
    string NodeId,
    TagRoleEnum Role,
    TagDirectionEnum Direction,
    string ConfiguredType,
    bool Enabled);
