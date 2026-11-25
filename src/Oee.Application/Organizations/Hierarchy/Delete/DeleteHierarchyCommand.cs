using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Hierarchy.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteHierarchy.</summary>
public sealed record DeleteHierarchyCommand(IReadOnlyCollection<EquipmentHierarchyDeleteItem> Items)
    : ICommand;
