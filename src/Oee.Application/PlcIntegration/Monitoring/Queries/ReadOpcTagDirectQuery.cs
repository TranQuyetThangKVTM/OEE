using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

public sealed record ReadOpcTagDirectQuery(
    Guid TagId,
    string ExpectedNodeId) : IRequest<OpcTagDirectRead>;
