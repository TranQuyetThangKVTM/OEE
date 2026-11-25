using MudBlazor;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.Queries;

namespace Oee.Web.Components.Pages.Plc;

public partial class OpcUaNodeBrowserDialog
{
    private readonly Dictionary<string, OpcUaBrowserTreeItem> _items = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _requests = new();
    private readonly SemaphoreSlim _readGate = new(1);
    private IReadOnlyCollection<TreeItemData<OpcUaBrowseNode>> _roots = [];
    private OpcUaBrowseNode? _selected;
    private bool _loading;
    private bool _disposed;
    private string? _error;

    [Microsoft.AspNetCore.Components.CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;
    [Microsoft.AspNetCore.Components.Parameter]
    public Guid ConnectionId { get; set; }
    [Microsoft.AspNetCore.Components.Parameter]
    public string? InitialNodeId { get; set; }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task<IReadOnlyCollection<OpcUaBrowseNode>> ReadAsync(string? parent, CancellationToken token)
    {
        await _readGate.WaitAsync(token);
        try { return await Plc.ExecuteAsync(new BrowseOpcUaNodesQuery(new(ConnectionId, parent)), token); }
        finally { if (!_disposed) _readGate.Release(); }
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        _error = null;
        _roots = [];
        _items.Clear();
        _selected = null;
        var token = _requests.Token;
        try
        {
            var nodes = await ReadAsync(null, token);
            token.ThrowIfCancellationRequested();
            _roots = nodes.Select(ToItem).ToArray();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { _error = "Không thể tải cây tag OPC UA. Kiểm tra kết nối tới server rồi thử lại."; }
        finally { if (!_disposed) _loading = false; }
    }

    private OpcUaBrowserTreeItem ToItem(OpcUaBrowseNode node)
    {
        var item = new OpcUaBrowserTreeItem(node);
        _items[node.NodeId] = item;
        if (node.IsVariable && node.NodeId == InitialNodeId) _selected = node;
        return item;
    }

    private async Task<IReadOnlyCollection<TreeItemData<OpcUaBrowseNode>>> LoadChildrenAsync(OpcUaBrowseNode? parent)
    {
        if (parent is null) return _roots;
        if (!_items.TryGetValue(parent.NodeId, out var item)) return [];
        if (item.Children is not null) return item.Children.Cast<TreeItemData<OpcUaBrowseNode>>().ToArray();
        return await LoadBranchAsync(item);
    }

    private async Task<IReadOnlyCollection<TreeItemData<OpcUaBrowseNode>>> LoadBranchAsync(OpcUaBrowserTreeItem item)
    {
        var token = _requests.Token;
        try
        {
            var nodes = await ReadAsync(item.Value!.NodeId, token);
            token.ThrowIfCancellationRequested();
            item.Children = nodes.Select(ToItem).ToArray();
            _error = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return []; }
        catch
        {
            _error = "Không thể tải nhánh tag. Bấm Thử lại nhánh hoặc kiểm tra kết nối tới server.";
            item.Children = [OpcUaBrowserTreeItem.Retry(item)];
        }
        return item.Children.Cast<TreeItemData<OpcUaBrowseNode>>().ToArray();
    }

    private async Task RetryBranchAsync(OpcUaBrowserTreeItem parent)
    {
        await LoadBranchAsync(parent);
        parent.Expanded = true;
    }

    private void Select(OpcUaBrowseNode? node) { if (node is { IsVariable: true }) _selected = node; }
    private void Confirm() { if (_selected is { IsVariable: true }) Dialog.Close(DialogResult.Ok(_selected)); }
    private void Cancel() => Dialog.Cancel();

    public void Dispose()
    {
        _disposed = true;
        _requests.Cancel();
        _requests.Dispose();
        _readGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
