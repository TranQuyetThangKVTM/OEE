using Oee.Application.Common;
using Oee.Application.Security;

namespace Oee.Application.PlcIntegration.Monitoring;

internal sealed class OpcTagMonitorContext(
    IOpcTagMonitorStore store,
    IOpcTagMonitorBuffer buffer,
    IOpcUaClientAdapter adapter,
    IClock clock,
    ICapabilityChecker? permissions = null)
    : IUseCaseContext
{
    internal IOpcTagMonitorStore Store => store;
    internal IOpcTagMonitorBuffer Buffer => buffer;
    internal IOpcUaClientAdapter Adapter => adapter;
    internal IClock Clock => clock;

    internal void EnsureReadAccess()
    {
        if (permissions is not null && !permissions.HasCapability(Capabilities.MasterDataRead))
            throw new UnauthorizedAccessException("Đăng nhập để giám sát tag OPC UA.");
    }
}
