namespace Oee.Application.Contracts.PlcIntegration;

public sealed record PlcFaultMappingItem(
    Guid Id,
    Guid? WorkUnitId,
    string Scope,
    string FaultCode,
    Guid DowntimeReasonId,
    string Reason,
    string? Description,
    bool IsActive,
    string RowVersion);
