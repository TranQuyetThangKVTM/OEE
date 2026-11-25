namespace Oee.Application.PlcIntegration;

internal sealed record PlcFaultValues(
    Guid? WorkUnitId,
    string FaultCode,
    Guid ReasonId,
    string? Description,
    bool Active);
