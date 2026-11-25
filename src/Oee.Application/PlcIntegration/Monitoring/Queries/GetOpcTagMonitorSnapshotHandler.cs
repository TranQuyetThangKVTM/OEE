using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

internal sealed class GetOpcTagMonitorSnapshotHandler(OpcTagMonitorContext context)
    : IRequestHandler<GetOpcTagMonitorSnapshotQuery, OpcTagMonitorSnapshot>
{
    public Task<OpcTagMonitorSnapshot> HandleAsync(
        GetOpcTagMonitorSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.EnsureReadAccess();

        return Task.FromResult(context.Buffer.Snapshot(context.Clock.UtcNow));
    }
}
