namespace Oee.Application.PlcIntegration.Monitoring;

public sealed record OpcTagMonitorSnapshot(
    DateTimeOffset At,
    int NoDataSeconds,
    IReadOnlyCollection<OpcTagObservation> Values,
    IReadOnlyDictionary<Guid, string> Connections);
