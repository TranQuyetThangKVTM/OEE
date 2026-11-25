using MudBlazor;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Web.Components.Pages.Plc;

internal sealed class OpcUaBrowserTreeItem : TreeItemData<OpcUaBrowseNode>
{
    public OpcUaBrowserTreeItem(OpcUaBrowseNode node) : base(node)
    {
        Text = node.DisplayName;
        Expandable = node.HasChildren;
    }

    private OpcUaBrowserTreeItem(OpcUaBrowserTreeItem parent) : base(null)
    {
        Text = "Thử lại nhánh";
        Expandable = false;
        RetryFor = parent;
    }

    public OpcUaBrowserTreeItem? RetryFor { get; }

    public static OpcUaBrowserTreeItem Retry(OpcUaBrowserTreeItem parent) => new(parent);
}
