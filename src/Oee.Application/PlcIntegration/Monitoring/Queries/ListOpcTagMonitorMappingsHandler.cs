using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

internal sealed class ListOpcTagMonitorMappingsHandler(OpcTagMonitorContext context)
    : IRequestHandler<ListOpcTagMonitorMappingsQuery, IReadOnlyCollection<OpcTagMonitorMapping>>
{
    public Task<IReadOnlyCollection<OpcTagMonitorMapping>> HandleAsync(
        ListOpcTagMonitorMappingsQuery request,
        CancellationToken cancellationToken)
    {
        context.EnsureReadAccess();

        return context.Store.ListAsync(cancellationToken);
    }
}
