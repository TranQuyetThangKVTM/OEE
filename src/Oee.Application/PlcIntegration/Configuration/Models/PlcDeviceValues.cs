using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration;

internal sealed record PlcDeviceValues(
    Guid ConnectionId,
    Guid WorkUnitId,
    Guid? ProductId,
    string ExternalId,
    CounterModeEnum CounterMode,
    FaultCodeFormatEnum FaultFormat,
    bool ZeroNoFault,
    int Debounce,
    string? Note,
    bool Active,
    IReadOnlyCollection<PlcTagValues> Tags);
