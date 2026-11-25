using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Plc;

public partial class OpcTagMonitor : IAsyncDisposable
{
    [Parameter] public bool ScreenMode { get; set; }
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Guid, OpcTagDirectRead> _direct = [];
    private IReadOnlyCollection<OpcTagMonitorMapping> _mappings = [];
    private IReadOnlyList<OpcTagMonitorRow> _rows = [];
    private OpcTagMonitorSnapshot? _snapshot;
    private MudTable<OpcTagMonitorRow>? _table;
    private readonly string _tableId = $"opc-tag-monitor-{Guid.NewGuid():N}";
    private readonly TableGrouping<OpcTagMonitorRow> _grouping = new(
        row => new(row.Mapping.DeviceId, row.Mapping.DeviceCode, row.Mapping.DeviceName));
    private Task? _loop;
    private string? _search;
    private string? _error;
    private string? _refreshError;
    private Guid? _deviceId;
    private Guid? _readingId;
    private bool _onlyProblems;
    private bool _loading;
    private bool _paused;
    private bool _disposed;

    private IReadOnlyList<OpcTagMonitorRow> FilteredRows => _rows.Where(row =>
        (!_deviceId.HasValue || row.Mapping.DeviceId == _deviceId)
        && (!_onlyProblems || row.Problem)
        && (string.IsNullOrWhiteSpace(_search) || string.Join(" ", row.Mapping.DeviceCode, row.Mapping.DeviceName,
            row.Mapping.ConnectionCode, row.RoleText, row.Mapping.NodeId, row.Observation?.Value, row.Status)
            .Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();

    protected override async Task OnInitializedAsync()
    {
        await ReloadMappingsAsync();
        if (!_disposed) _loop = RefreshLoopAsync(_lifetime.Token);
    }

    private async Task ReloadMappingsAsync()
    {
        if (_disposed || _loading) return;
        _loading = true;
        _error = null;
        try
        {
            var mappings = await Requests.ExecuteAsync(new ListOpcTagMonitorMappingsQuery(), _lifetime.Token);
            if (_disposed) return;
            foreach (var id in _direct.Keys.Where(id => !mappings.Any(mapping => mapping.Id == id && mapping.NodeId == _direct[id].NodeId)).ToArray())
                _direct.Remove(id);
            _mappings = mappings;
            await RefreshSnapshotAsync(force: true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { if (!_disposed) _error = "Không thể tải ánh xạ tag. Hãy thử Tải lại ánh xạ."; }
        finally { _loading = false; }
    }

    private async Task RefreshSnapshotAsync(bool force = false)
    {
        var snapshot = await Requests.ExecuteAsync(new GetOpcTagMonitorSnapshotQuery(), _lifetime.Token);
        if (_disposed || (_paused && !force)) return;
        _refreshError = null;
        _snapshot = snapshot;
        var values = snapshot.Values.ToDictionary(value => (value.DeviceId, value.NodeId, value.Role));
        _rows = _mappings.Select(mapping => new OpcTagMonitorRow(mapping,
            values.GetValueOrDefault((mapping.DeviceId, mapping.NodeId, mapping.Role)),
            snapshot.Connections.GetValueOrDefault(mapping.DeviceId, "Unknown"), snapshot.At, snapshot.NoDataSeconds)).ToArray();
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await InvokeAsync(async () =>
                {
                    if (_disposed || _paused) return;
                    try { await RefreshSnapshotAsync(); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                    catch { _refreshError = "Màn hình chưa nhận được bản cập nhật mới. Đang thử lại…"; }
                    if (!_disposed) StateHasChanged();
                });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task TogglePauseAsync()
    {
        _paused = !_paused;
        if (!_paused)
        {
            try { await RefreshSnapshotAsync(); _error = null; }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch { _error = "Không thể làm mới màn hình. Hãy thử lại."; }
        }
    }

    private async Task ReadDirectAsync(OpcTagMonitorMapping mapping)
    {
        if (_disposed || _readingId.HasValue || !mapping.Enabled) return;
        _readingId = mapping.Id;
        _error = null;
        try
        {
            var read = await Requests.ExecuteAsync(new ReadOpcTagDirectQuery(mapping.Id, mapping.NodeId), _lifetime.Token);
            if (!_disposed && _mappings.Any(current => current.Id == mapping.Id && current.NodeId == read.NodeId)) _direct[mapping.Id] = read;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { if (!_disposed) _error = "Không thể đọc tag. Hãy tải lại ánh xạ và kiểm tra kết nối."; }
        finally { _readingId = null; }
    }

    private async Task OpenScreenAsync()
    {
        var wasPaused = _paused;
        _paused = true;
        try
        {
            var dialog = await Dialogs.ShowAsync<OpcTagMonitorScreenDialog>("Giám sát tag OPC UA",
                new DialogOptions { FullScreen = true, CloseOnEscapeKey = true, CloseButton = true });
            await dialog.Result;
        }
        finally { _paused = wasPaused; }
    }

    private static string Utc(DateTimeOffset? value) => value.HasValue && value.Value != default
        ? value.Value.UtcDateTime.ToString("dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : "—";
    private static string RowClass(OpcTagMonitorRow row, int index) => row.Problem ? "opc-tag-row--problem" : row.Changed ? "opc-tag-row--changed" : string.Empty;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _lifetime.CancelAsync();
        if (_loop is not null) await _loop;
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
