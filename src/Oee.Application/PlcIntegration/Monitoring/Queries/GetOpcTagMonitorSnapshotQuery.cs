using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

public sealed record GetOpcTagMonitorSnapshotQuery : IRequest<OpcTagMonitorSnapshot>;
