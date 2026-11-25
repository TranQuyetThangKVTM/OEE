namespace Oee.Application.PlcIntegration.Monitoring;

public interface IOpcTagMonitorStore
{
    Task<IReadOnlyCollection<OpcTagMonitorMapping>> ListAsync(CancellationToken ct);

    Task<OpcTagMonitorTarget?> FindAsync(Guid tagId, CancellationToken ct);
}
