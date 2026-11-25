using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

public sealed record ListOpcTagMonitorMappingsQuery : IRequest<IReadOnlyCollection<OpcTagMonitorMapping>>;
