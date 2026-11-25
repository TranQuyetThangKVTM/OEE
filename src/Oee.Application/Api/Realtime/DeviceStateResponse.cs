namespace Oee.Application.Contracts.Api;

/// <summary>DTO trạng thái một WorkUnit, tách OperationalState khỏi ConnectivityState.</summary>
public sealed record DeviceStateResponse(
    Guid WorkUnitId,
    string Code,
    string Name,
    string OperationalState,
    string ConnectivityState,
    decimal? Oee);
