using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.Areas.Delete;
using Oee.Application.Organizations.Enterprises.Delete;
using Oee.Application.Organizations.Hierarchy.Delete;
using Oee.Application.Organizations.Sites.Delete;
using Oee.Application.Organizations.WorkCenters.Delete;
using Oee.Application.Organizations.WorkUnits.Delete;
using Oee.Web.Components.Presentation;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Master;

public partial class EquipmentHierarchy
{
    private const int RootPageSize = 25;
    private const int ChildPageSize = 50;
    private const int MaxDeleteSelection = 200;
    private readonly Dictionary<EquipmentHierarchyNodeKey, EquipmentHierarchyTreeItem> _nodes = [];
    private readonly Dictionary<EquipmentHierarchyNodeKey, EquipmentHierarchyNode> _checked = [];
    private CancellationTokenSource _requests = new();
    private IReadOnlyCollection<TreeItemData<EquipmentHierarchyNode>> _roots = [];
    private EquipmentHierarchyNode? _selected;
    private readonly string?[] _searches = new string?[5];
    private EquipmentHierarchyQuery _appliedQuery = new();
    private string? _error;
    private int _rootPage = 1;
    private int _totalRoots;
    private bool _loading;
    private bool _mutating;
    private bool _resetView;
    private bool _openFirstBranch = true;
    private bool _disposed;
    private bool _busy => _loading || _mutating;
    private int RootPageCount => Math.Max(1, (int)Math.Ceiling((double)_totalRoots / RootPageSize));
    private bool AllVisibleChecked => VisibleNodes().Any() && VisibleNodes().All(x => _checked.ContainsKey(Key(x)));

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        var restore = _resetView ? [] : _nodes.Where(x => x.Value.Expanded).Select(x => x.Key).ToHashSet();
        var selectedKey = _selected is null ? (EquipmentHierarchyNodeKey?)null : Key(_selected);
        _resetView = false;
        _requests.Cancel();
        _requests.Dispose();
        _requests = new();
        var token = _requests.Token;
        _loading = true;
        _checked.Clear();
        _error = null;
        _appliedQuery = new(Page: _rootPage, PageSize: RootPageSize,
            EnterpriseSearch: NormalizeSearch(_searches[0]), SiteSearch: NormalizeSearch(_searches[1]),
            AreaSearch: NormalizeSearch(_searches[2]), WorkCenterSearch: NormalizeSearch(_searches[3]),
            WorkUnitSearch: NormalizeSearch(_searches[4]));
        _nodes.Clear();
        _roots = [];
        _selected = null;
        try
        {
            var result = await TreeQuery.GetAsync(_appliedQuery, token);
            token.ThrowIfCancellationRequested();
            _totalRoots = result.TotalCount;
            if (_rootPage > RootPageCount)
            {
                _rootPage = RootPageCount;
                _appliedQuery = _appliedQuery with { Page = _rootPage };
                result = await TreeQuery.GetAsync(_appliedQuery, token);
                token.ThrowIfCancellationRequested();
            }
            _roots = result.Items.Select(ToItem).ToArray();
            foreach (var root in _roots.Cast<EquipmentHierarchyTreeItem>())
                await RestoreAsync(root, restore, token);
            if (selectedKey.HasValue && _nodes.TryGetValue(selectedKey.Value, out var selected)) _selected = selected.Value;
            if (_openFirstBranch)
            {
                _openFirstBranch = false;
                var root = _roots.Select(x => x.Value!).FirstOrDefault(x => x.Status == "Active" && x.ChildCount > 0)
                    ?? _roots.Select(x => x.Value!).FirstOrDefault(x => x.ChildCount > 0) ?? _roots.FirstOrDefault()?.Value;
                if (root is not null) await OpenPathAsync(root, token);
            }
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            LogRootLoadFailed(Logger, exception);
            _error = "Không thể tải cây cơ cấu tổ chức. Hãy thử làm mới.";
        }
        finally
        {
            if (!token.IsCancellationRequested && !_disposed) _loading = false;
        }
    }

    private EquipmentHierarchyTreeItem ToItem(EquipmentHierarchyNode node)
    {
        var item = new EquipmentHierarchyTreeItem(node);
        _nodes[Key(node)] = item;
        return item;
    }

    private async Task RestoreAsync(EquipmentHierarchyTreeItem item, HashSet<EquipmentHierarchyNodeKey> restore, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!restore.Contains(Key(item.Value!)) || !item.Expandable) return;
        await LoadChildrenAsync(item, token);
        item.Expanded = true;
        foreach (var child in item.Children?.OfType<EquipmentHierarchyTreeItem>().Where(x => x.MoreFor is null) ?? [])
            await RestoreAsync(child, restore, token);
    }

    // MudTreeView invokes this when its native toggle expands an unloaded item.
    private async Task<IReadOnlyCollection<TreeItemData<EquipmentHierarchyNode>>> LoadServerDataAsync(EquipmentHierarchyNode? parent)
    {
        if (parent is null || !_nodes.TryGetValue(Key(parent), out var item)) return [];
        await LoadChildrenAsync(item, _requests.Token);
        return item.Children?.Cast<TreeItemData<EquipmentHierarchyNode>>().ToArray() ?? [];
    }

    private async Task LoadChildrenAsync(EquipmentHierarchyTreeItem item, CancellationToken token, bool append = false)
    {
        if (!item.Expandable || item.Loading || (!append && item.Children is not null)) return;
        var parent = item.Value!;
        item.Loading = true;
        _error = null;
        try
        {
            var result = await TreeQuery.GetAsync(_appliedQuery with
            {
                ParentKind = parent.Kind, ParentId = parent.Id, Page = item.NextPage, PageSize = ChildPageSize,
            }, token);
            token.ThrowIfCancellationRequested();
            var children = append ? item.Children?.OfType<EquipmentHierarchyTreeItem>().Where(x => x.MoreFor is null).ToList() ?? [] : [];
            children.AddRange(result.Items.Select(ToItem));
            item.NextPage++;
            if (children.Count < result.TotalCount) children.Add(EquipmentHierarchyTreeItem.More(parent));
            item.Children = children;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            LogBranchLoadFailed(Logger, parent.Kind, parent.Id, exception);
            _error = "Không thể tải nhánh đang chọn. Hãy thử lại hoặc làm mới.";
            // A retry child keeps the native branch expandable without throwing into the circuit.
            item.Children ??= [EquipmentHierarchyTreeItem.More(parent, retry: true)];
        }
        finally { item.Loading = false; }
    }

    private async Task LoadMoreAsync(EquipmentHierarchyNode parent)
    {
        if (_busy || !_nodes.TryGetValue(Key(parent), out var item)) return;
        await LoadChildrenAsync(item, _requests.Token, append: true);
    }

    private void ExpandedChanged(EquipmentHierarchyTreeItem item, bool expanded)
    {
        item.Expanded = expanded;
        Select(item.Value);
    }

    private async Task OpenPathAsync(EquipmentHierarchyNode start, CancellationToken token)
    {
        var current = start;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Select(current);
            var item = _nodes[Key(current)];
            if (!item.Expandable) break;
            await LoadChildrenAsync(item, token);
            token.ThrowIfCancellationRequested();
            item.Expanded = true;
            if (current.Kind == EquipmentHierarchyKind.WorkCenter) break;
            var children = item.Children?.OfType<EquipmentHierarchyTreeItem>().Where(x => x.MoreFor is null).Select(x => x.Value!).ToArray() ?? [];
            var next = children.FirstOrDefault(x => x.Status == "Active" && x.ChildCount > 0)
                ?? children.FirstOrDefault(x => x.ChildCount > 0) ?? children.FirstOrDefault();
            if (next is null) break;
            current = next;
        }
    }

    private Task ApplySearchAsync(EquipmentHierarchyKind kind, string? value)
    {
        if (_mutating) return Task.CompletedTask;
        _searches[(int)kind] = value;
        _rootPage = 1;
        _resetView = true;
        _openFirstBranch = _searches.Any(x => !string.IsNullOrWhiteSpace(x));
        return ReloadAsync();
    }

    private static string? NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task ChangeRootPageAsync(int page)
    {
        _rootPage = page;
        _resetView = true;
        _openFirstBranch = true;
        return ReloadAsync();
    }

    private void Select(EquipmentHierarchyNode? node) => _selected = node;

    private List<EquipmentHierarchyNode> SelectedPath()
    {
        var path = new List<EquipmentHierarchyNode>();
        var current = _selected;
        while (current is not null)
        {
            path.Add(current);
            if (current.Kind == EquipmentHierarchyKind.Enterprise || !current.ParentId.HasValue) break;
            current = _nodes.GetValueOrDefault(new(current.Kind - 1, current.ParentId.Value))?.Value;
        }
        path.Reverse();
        return path;
    }

// Thêm
    private async Task CreateAsync(EquipmentHierarchyNode? parent)
    {
        if (_busy || parent?.Kind == EquipmentHierarchyKind.WorkUnit || (parent is not null && parent.Status != "Active")) return;
        var kind = parent is null ? EquipmentHierarchyKind.Enterprise : parent.Kind + 1;
        var parameters = new DialogParameters<MasterDataCreateDialog>
        {
            { x => x.Kind, CreateKind(kind) },
            { x => x.ParentId, parent?.Id },
            { x => x.LockParent, parent is not null },
        };
        var dialog = await DialogService.ShowAsync<MasterDataCreateDialog>($"Thêm {KindLabel(kind)}", parameters, EditorOptions());
        if (await dialog.Result is not { Canceled: false }) return;
        await ReloadAsync();
        if (parent is not null && _nodes.TryGetValue(Key(parent), out var refreshed))
        {
            await LoadChildrenAsync(refreshed, _requests.Token);
            refreshed.Expanded = true;
            Select(refreshed.Value);
        }
    }
// Sửa
    private async Task EditAsync(EquipmentHierarchyNode node)
    {
        if (_busy) return;
        Select(node);
        var parameters = new DialogParameters<MasterDataEditDialog> { { x => x.Kind, EditKind(node.Kind) }, { x => x.Id, node.Id } };
        var dialog = await DialogService.ShowAsync<MasterDataEditDialog>($"Sửa {KindLabel(node.Kind)}", parameters, EditorOptions());
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }
// Active/Inactive
    private async Task ChangeStatusAsync(EquipmentHierarchyNode node)
    {
        if (_busy) return;
        var parameters = new DialogParameters<MasterStatusDialog> { { x => x.Kind, EditKind(node.Kind) }, { x => x.Id, node.Id } };
        var dialog = await DialogService.ShowAsync<MasterStatusDialog>("Trạng thái hoạt động", parameters, EditorOptions());
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }
// Xóa
    private async Task DeleteAsync(EquipmentHierarchyNode node)
    {
        if (_busy || node.ChildCount > 0) return;
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, $"Xóa {KindLabel(node.Kind)}" },
            { x => x.Message, $"Bạn muốn xóa {node.Code} — {node.Name}? Bản ghi có dữ liệu phụ thuộc sẽ không thể xóa." },
            { x => x.ConfirmText, "Xóa" },
        };
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận xóa", parameters, EditorOptions());
        if (await dialog.Result is not { Canceled: false, Data: true } || _busy) return;
        _mutating = true;
        try
        {
            // RowVersion và mọi dependency rule vẫn được command hiện có kiểm tra tại server.
            await (node.Kind switch
            {
                EquipmentHierarchyKind.Enterprise => Commands.ExecuteAsync(new DeleteEnterpriseCommand(node.Id, node.RowVersion), _requests.Token),
                EquipmentHierarchyKind.Site => Commands.ExecuteAsync(new DeleteSiteCommand(node.Id, node.RowVersion), _requests.Token),
                EquipmentHierarchyKind.Area => Commands.ExecuteAsync(new DeleteAreaCommand(node.Id, node.RowVersion), _requests.Token),
                EquipmentHierarchyKind.WorkCenter => Commands.ExecuteAsync(new DeleteWorkCenterCommand(node.Id, node.RowVersion), _requests.Token),
                EquipmentHierarchyKind.WorkUnit => Commands.ExecuteAsync(new DeleteWorkUnitCommand(node.Id, node.RowVersion), _requests.Token),
                _ => throw new InvalidOperationException("Cấp tổ chức không hợp lệ."),
            });
            Snackbar.Add($"Đã xóa {node.Code}.", Severity.Success);
            await ReloadAsync();
        }
        catch (OperationCanceledException) when (_requests.IsCancellationRequested) { }
        catch (Exception exception)
        {
            LogDeleteFailed(Logger, node.Kind, node.Id, exception);
            _error = "Không thể xóa: bản ghi có dữ liệu phụ thuộc hoặc đã được thay đổi. Hãy làm mới và thử lại.";
        }
        finally { _mutating = false; }
    }

    private IEnumerable<EquipmentHierarchyNode> VisibleNodes() => VisibleNodes(_roots);

    private static IEnumerable<EquipmentHierarchyNode> VisibleNodes(IEnumerable<ITreeItemData<EquipmentHierarchyNode>> items)
    {
        foreach (var item in items.OfType<EquipmentHierarchyTreeItem>().Where(x => x.MoreFor is null))
        {
            if (item.Value is { } node) yield return node;
            if (item.Expanded && item.Children is not null)
                foreach (var child in VisibleNodes(item.Children)) yield return child;
        }
    }

    private void CheckNode(EquipmentHierarchyNode node, bool value)
    {
        if (_busy) return;
        if (!value) _checked.Remove(Key(node));
        else if (_checked.Count < MaxDeleteSelection) _checked[Key(node)] = node;
        else Snackbar.Add($"Mỗi lần chọn tối đa {MaxDeleteSelection} dòng.", Severity.Warning);
    }

    private void CheckVisible(bool value)
    {
        if (_busy) return;
        var visible = VisibleNodes().ToArray();
        if (!value) { foreach (var node in visible) _checked.Remove(Key(node)); return; }
        if (_checked.Keys.Union(visible.Select(Key)).Count() > MaxDeleteSelection)
        {
            Snackbar.Add($"Mỗi lần chọn tối đa {MaxDeleteSelection} dòng. Hãy chọn từng nhóm nhỏ hơn.", Severity.Warning);
            return;
        }
        foreach (var node in visible) _checked[Key(node)] = node;
    }

    private void ClearChecked() { if (!_busy) _checked.Clear(); }

    private async Task DeleteCheckedAsync()
    {
        if (_busy || _checked.Count == 0) return;
        var selected = _checked.Values.ToArray();
        var preview = string.Join(", ", selected.Take(12).Select(x => $"{KindLabel(x.Kind)} {x.Code}"));
        if (selected.Length > 12) preview += $" và {selected.Length - 12} dòng khác";
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, $"Xóa {selected.Length} dòng đã chọn?" },
            { x => x.Message, $"Các dòng: {preview}. Chỉ xóa các dòng đã tick, không tự xóa con chưa chọn. Dữ liệu đã sử dụng sẽ bị chặn. Nếu một dòng không hợp lệ, toàn bộ lượt xóa sẽ được hủy." },
            { x => x.ConfirmText, "Xóa đã chọn" },
        };
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận xóa nhiều dòng", parameters, EditorOptions());
        if (await dialog.Result is not { Canceled: false, Data: true } || _busy) return;
        _mutating = true;
        _error = null;
        try
        {
            await Commands.ExecuteAsync(new DeleteHierarchyCommand(selected.Select(x => new EquipmentHierarchyDeleteItem(x.Kind, x.Id, x.RowVersion)).ToArray()), _requests.Token);
            Snackbar.Add($"Đã xóa {selected.Length} dòng.", Severity.Success);
            await ReloadAsync();
        }
        catch (OperationCanceledException) when (_requests.IsCancellationRequested) { }
        catch (Exception exception)
        {
            LogBulkDeleteFailed(Logger, selected.Length, exception);
            _error = "Không thể xóa các dòng đã chọn; toàn bộ lượt xóa đã được hủy. Hãy kiểm tra cấp con chưa chọn, dữ liệu phụ thuộc hoặc làm mới nếu bản ghi đã thay đổi.";
        }
        finally { _mutating = false; }
    }

    private bool IsLoading(EquipmentHierarchyNode node) => _nodes.GetValueOrDefault(Key(node))?.Loading == true;
    private static Color NodeColor(EquipmentHierarchyKind kind) => kind switch
    {
        EquipmentHierarchyKind.Enterprise => Color.Info,
        EquipmentHierarchyKind.Site => Color.Success,
        EquipmentHierarchyKind.Area => Color.Secondary,
        EquipmentHierarchyKind.WorkCenter => Color.Warning,
        _ => Color.Default,
    };
    private static EquipmentHierarchyNodeKey Key(EquipmentHierarchyNode node) => new(node.Kind, node.Id);
    private static string KindLabel(EquipmentHierarchyKind kind) => kind switch
    {
        EquipmentHierarchyKind.WorkCenter => "Work Center",
        EquipmentHierarchyKind.WorkUnit => "Work Unit",
        _ => kind.ToString(),
    };
    private static string LevelClass(EquipmentHierarchyKind kind) => kind.ToString().ToLowerInvariant();
    private static string NodeIcon(EquipmentHierarchyKind kind) => kind switch
    {
        EquipmentHierarchyKind.Enterprise => Icons.Material.Filled.Business,
        EquipmentHierarchyKind.Site => Icons.Material.Filled.Factory,
        EquipmentHierarchyKind.Area => Icons.Material.Filled.Layers,
        EquipmentHierarchyKind.WorkCenter => Icons.Material.Filled.AccountTree,
        _ => Icons.Material.Filled.Settings,
    };
    private static string StatusText(EquipmentHierarchyNode node) => node.Status != "Active" ? node.Status
        : node.Kind == EquipmentHierarchyKind.WorkUnit ? node.OperationalState ?? "Unknown" : node.Status;
    private static SemanticKind StatusKind(EquipmentHierarchyNode node) => StatusText(node) switch
    {
        "Active" or "Running" => SemanticKind.Success,
        "Stopped" => SemanticKind.Error,
        "Setup" or "Maintenance" or "Cleaning" or "Idle" => SemanticKind.Warning,
        "Alarm" => SemanticKind.Error,
        _ => SemanticKind.Neutral,
    };
    private static MasterCreateKind CreateKind(EquipmentHierarchyKind kind) => kind switch
    {
        EquipmentHierarchyKind.Enterprise => MasterCreateKind.Enterprise,
        EquipmentHierarchyKind.Site => MasterCreateKind.Site,
        EquipmentHierarchyKind.Area => MasterCreateKind.Area,
        EquipmentHierarchyKind.WorkCenter => MasterCreateKind.WorkCenter,
        _ => MasterCreateKind.WorkUnit,
    };
    private static MasterEditKind EditKind(EquipmentHierarchyKind kind) => kind switch
    {
        EquipmentHierarchyKind.Enterprise => MasterEditKind.Enterprise,
        EquipmentHierarchyKind.Site => MasterEditKind.Site,
        EquipmentHierarchyKind.Area => MasterEditKind.Area,
        EquipmentHierarchyKind.WorkCenter => MasterEditKind.WorkCenter,
        _ => MasterEditKind.WorkUnit,
    };
    private static DialogOptions EditorOptions() => new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unable to load equipment hierarchy roots")]
    private static partial void LogRootLoadFailed(ILogger logger, Exception exception);
    [LoggerMessage(Level = LogLevel.Error, Message = "Unable to load hierarchy children for {Kind} {Id}")]
    private static partial void LogBranchLoadFailed(ILogger logger, EquipmentHierarchyKind kind, Guid id, Exception exception);
    [LoggerMessage(Level = LogLevel.Error, Message = "Unable to delete hierarchy node {Kind} {Id}")]
    private static partial void LogDeleteFailed(ILogger logger, EquipmentHierarchyKind kind, Guid id, Exception exception);
    [LoggerMessage(Level = LogLevel.Error, Message = "Unable to delete {Count} selected hierarchy nodes")]
    private static partial void LogBulkDeleteFailed(ILogger logger, int count, Exception exception);

    public void Dispose()
    {
        _disposed = true;
        _requests.Cancel();
        _requests.Dispose();
        GC.SuppressFinalize(this);
    }
}
