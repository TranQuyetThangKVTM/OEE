using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration.Monitoring;

/// <summary>Bản sao giá trị nhận tại OEE, độc lập checkpoint và số liệu sản xuất.</summary>
public sealed record OpcTagObservation(
    Guid DeviceId,
    string NodeId,
    TagRoleEnum Role,
    string Value,
    string ActualType,
    bool IsGood,
    string StatusCode,
    DateTimeOffset SourceAt,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ChangedAt,
    bool FromReadRefresh);
