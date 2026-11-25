using Oee.Application.Contracts.Modules;
using Oee.Domain.Common;

namespace Oee.Infrastructure.Operations;

internal sealed class EquipmentHierarchyNodeProjection
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public EquipmentHierarchyKind Kind { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int ChildCount { get; init; }
    public RecordStatusEnum Status { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public byte[] RowVersion { get; init; } = [];
    public OperationalStateEnum? OperationalState { get; init; }
    public ConnectivityStateEnum? ConnectivityState { get; init; }
}
