using MudBlazor;
using Oee.Application.Contracts.Modules;

namespace Oee.Web.Components.Pages.Master;

internal sealed class EquipmentHierarchyTreeItem : TreeItemData<EquipmentHierarchyNode>
{
    public EquipmentHierarchyTreeItem(EquipmentHierarchyNode? node) : base(node)
    {
        Text = node?.Name;
        Expandable = node is { ChildCount: > 0 } && node.Kind != EquipmentHierarchyKind.WorkUnit;
    }

    public bool Loading { get; set; }
    public int NextPage { get; set; } = 1;
    public EquipmentHierarchyNode? MoreFor { get; private init; }
    public bool Retry { get; private init; }

    public static EquipmentHierarchyTreeItem More(EquipmentHierarchyNode parent, bool retry = false) => new(null)
    {
        MoreFor = parent,
        Retry = retry,
        Text = retry ? "Thử lại nhánh" : "Xem thêm con"
    };
}
