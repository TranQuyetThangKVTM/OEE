using Oee.Domain.Common;

namespace Oee.Domain.PlcIntegration;

/// <summary>Lịch sử transition kết nối của một OpcUaConnection.</summary>
public sealed class PlcSessionEventEntity : Entity
{
    public Guid OpcUaConnectionId { get; private set; }
    public OpcUaConnectionEntity OpcUaConnection { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public ConnectivityStateEnum State { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public string? Detail { get; private set; }

    private PlcSessionEventEntity()
    {
    }

    public static PlcSessionEventEntity Create(Guid opcUaConnectionId, DateTimeOffset occurredAt, ConnectivityStateEnum state, string message, string? detail = null)
    {
        return new PlcSessionEventEntity
        {
            OpcUaConnectionId = DomainGuard.RequiredId(opcUaConnectionId),
            OccurredAt = occurredAt.ToUniversalTime(),
            State = DomainGuard.Defined(state),
            Message = DomainGuard.Required(message, 1000),
            Detail = DomainGuard.Optional(detail, 4000)
        };
    }
}
