namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Các lookup cấu hình PLC được tải một lần bằng projection, tránh N+1 trong dialog.</summary>
public sealed record PlcConfigurationLookups(
    IReadOnlyCollection<PlcLookup> Connections,
    IReadOnlyCollection<PlcLookup> WorkUnits,
    IReadOnlyCollection<PlcLookup> Products,
    IReadOnlyCollection<PlcLookup> DowntimeReasons);
