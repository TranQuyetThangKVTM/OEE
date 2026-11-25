namespace Oee.Application.Contracts.Api;

/// <summary>Trạng thái production của toàn bộ máy tại một thời điểm UTC.</summary>
public sealed record ProductionStatusResponse(
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<DeviceStateResponse> Machines);
