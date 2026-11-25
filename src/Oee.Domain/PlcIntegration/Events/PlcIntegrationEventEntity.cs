using Oee.Domain.Common;

namespace Oee.Domain.PlcIntegration;

/// <summary>Event tích hợp persisted để RCA sau restart, tách với live ring buffer trên UI.</summary>
public sealed class PlcIntegrationEventEntity : Entity
{
    public Guid? PlcDeviceMappingId { get; private set; }
    public PlcDeviceMappingEntity? PlcDeviceMapping { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public IntegrationEventLevelEnum Level { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? Detail { get; private set; }

    private PlcIntegrationEventEntity()
    {
    }

    public static PlcIntegrationEventEntity Create(DateTimeOffset occurredAt, IntegrationEventLevelEnum level, string kind, string message,
        Guid? plcDeviceMappingId = null, string? detail = null)
    {
        return new PlcIntegrationEventEntity
        {
            PlcDeviceMappingId = DomainGuard.OptionalId(plcDeviceMappingId),
            OccurredAt = occurredAt.ToUniversalTime(),
            Level = DomainGuard.Defined(level),
            Kind = DomainGuard.Required(kind, 64),
            Message = DomainGuard.Required(message, 1000),
            Detail = DomainGuard.Optional(detail, 4000)
        };
    }
}
