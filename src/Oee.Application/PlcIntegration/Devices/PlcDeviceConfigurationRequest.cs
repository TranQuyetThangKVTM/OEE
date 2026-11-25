namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Request lưu mapping và toàn bộ tag trong một transaction; RowVersion chống ghi đè config.</summary>
public sealed record PlcDeviceConfigurationRequest(
    Guid ConnectionId,
    Guid WorkUnitId,
    Guid? DefaultProductId,
    string ExternalDeviceId,
    string CounterMode,
    string FaultCodeFormat,
    bool ZeroFaultCodeMeansNoFault,
    int DebounceSamples,
    string? Note,
    bool IsActive,
    IReadOnlyCollection<PlcTagConfiguration> Tags,
    string? RowVersion = null);
