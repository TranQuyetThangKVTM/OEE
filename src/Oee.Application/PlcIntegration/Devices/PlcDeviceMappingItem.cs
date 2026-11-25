namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Dòng device mapping gồm config, tag count và runtime connectivity.</summary>
public sealed record PlcDeviceMappingItem(
    Guid Id,
    Guid ConnectionId,
    string Connection,
    Guid WorkUnitId,
    string WorkUnit,
    Guid? DefaultProductId,
    string? DefaultProduct,
    string ExternalDeviceId,
    string CounterMode,
    string FaultCodeFormat,
    bool ZeroFaultCodeMeansNoFault,
    int DebounceSamples,
    string? Note,
    int ReadTagCount,
    int WriteTagCount,
    DateTimeOffset? LastReadAt,
    string Connectivity,
    bool IsActive,
    string RowVersion,
    DateTimeOffset? LastReceivedAt = null,
    bool CounterDiscontinuityPending = false,
    decimal? LastTotalCounter = null,
    decimal? LastDefectCounter = null,
    string? WorkUnitName = null,
    string? DefaultProductName = null);
