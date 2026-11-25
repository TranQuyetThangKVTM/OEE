namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Request fault mapping global khi WorkUnitId null hoặc riêng thiết bị khi có ID.</summary>
public sealed record PlcFaultMappingRequest(
    Guid? WorkUnitId,
    string FaultCode,
    Guid DowntimeReasonId,
    string? Description,
    bool IsActive,
    string? RowVersion = null);
