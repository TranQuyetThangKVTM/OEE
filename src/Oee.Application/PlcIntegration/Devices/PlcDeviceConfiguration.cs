namespace Oee.Application.Contracts.PlcIntegration;

public sealed record PlcDeviceConfiguration(
    PlcDeviceMappingItem Mapping,
    IReadOnlyCollection<PlcTagConfiguration> Tags);
