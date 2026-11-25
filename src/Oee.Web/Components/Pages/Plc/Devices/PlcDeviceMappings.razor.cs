using System.Globalization;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.Queries;
using Oee.Application.PlcIntegration.Configuration.Devices.Delete;
using Oee.Application.PlcIntegration.Configuration.Devices.Queries;
using Oee.Application.PlcIntegration.Configuration.Devices.Update;
using Oee.Application.PlcIntegration.Configuration.Overview.Queries;
using Oee.Web.Components.Presentation;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Plc;

public partial class PlcDeviceMappings
{
    private MudTable<PlcDeviceMappingItem>? _table;
    private IReadOnlyCollection<OpcUaConnectionItem> _connections = [];
    private bool _connectionsLoaded;
    private string? _search;
    private bool? _activeFilter = true;
    private string? _error;
    private bool _openingEditor;
    private int _totalCount;
    private int _configuredOpcUa;

    private async Task LoadConnectionsAsync(CancellationToken cancellationToken)
    {
        // Trang chỉ cần connection để lọc; danh mục sản phẩm/lý do dừng tải khi mở editor.
        try
        {
            _connections = (await Plc.ExecuteAsync(new ListOpcUaConnectionsQuery(new PlcListQuery(PageSize: 100)), cancellationToken)).Items;
            _connectionsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải danh mục PLC.";
        }
    }

    private async Task<TableData<PlcDeviceMappingItem>> LoadAsync(
        TableState tableState,
        CancellationToken cancellationToken)
    {
        // Mapping được search/filter/sort/page phía server, không tải toàn bộ cấu hình vào browser.
        try
        {
            _error = null;
            PagedResult<PlcDeviceMappingItem> result = await Plc.ExecuteAsync(new ListPlcDeviceMappingsQuery(new PlcListQuery(
                    _search,
                    _activeFilter?.ToString(),
                    null,
                    tableState.SortLabel,
                    tableState.SortDirection == SortDirection.Descending,
                    tableState.Page + 1,
                    tableState.PageSize)), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _totalCount = result.TotalCount;
            _configuredOpcUa = result.Items.Count(item => item.ReadTagCount > 0 || item.WriteTagCount > 0);
            // Cùng service EF: chạy tuần tự, tránh lookup và MudTable truy vấn chung context cùng lúc.
            if (!_connectionsLoaded) await LoadConnectionsAsync(cancellationToken);
            await InvokeAsync(StateHasChanged);
            return new TableData<PlcDeviceMappingItem>
            {
                Items = result.Items,
                TotalItems = result.TotalCount
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải mapping.";
            return new TableData<PlcDeviceMappingItem> { Items = [], TotalItems = 0 };
        }
    }

    private async Task ApplyFiltersAsync()
    {
        if (_table is null) return;
        if (_table.CurrentPage != 0) _table.NavigateTo(0);
        else await _table.ReloadServerData();
    }

    private Task ReloadAsync()
    {
        _connectionsLoaded = false;
        return _table?.ReloadServerData() ?? Task.CompletedTask;
    }

    private async Task OpenMappingEditorAsync(Guid? mappingId)
    {
        if (_openingEditor) return;
        _openingEditor = true;
        try
        {
            var lookups = await Plc.ExecuteAsync(new GetPlcLookupsQuery(), CancellationToken.None);
            var parameters = new DialogParameters<PlcMappingEditorDialog>
            {
                { component => component.MappingId, mappingId },
                { component => component.Lookups, lookups }
            };
            var dialog = await DialogService.ShowAsync<PlcMappingEditorDialog>(
                mappingId.HasValue ? "Cập nhật ánh xạ PLC" : "Thêm ánh xạ PLC", parameters,
                new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Small, BackdropClick = false });
            if (await dialog.Result is { Canceled: false }) await ReloadAsync();
        }
        catch { _error = "Không thể tải hoặc lưu ánh xạ PLC. Hãy thử lại."; }
        finally { _openingEditor = false; }
    }

    private async Task OpenEditorAsync(Guid? mappingId)
    {
        if (_openingEditor) return;
        _openingEditor = true;
        PlcConfigurationLookups lookups;
        try
        {
            // Không tải danh mục editor trong đường tải ban đầu của bảng.
            lookups = await Plc.ExecuteAsync(new GetPlcLookupsQuery(), CancellationToken.None);
        }
        catch (Exception)
        {
            _error = "Không thể tải danh mục cấu hình OPC UA. Vui lòng thử lại.";
            return;
        }
        finally
        {
            _openingEditor = false;
        }

        DialogParameters<OpcUaConfigurationDialog> parameters = new()
        {
            { component => component.MappingId, mappingId },
            { component => component.Lookups, lookups },
            { component => component.Connections, _connections }
        };
        DialogOptions options = new()
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Large,
            CloseButton = false,
            BackdropClick = false
        };
        IDialogReference dialog = await DialogService.ShowAsync<OpcUaConfigurationDialog>(
            "Cấu hình OPC UA",
            parameters,
            options);
        DialogResult? result = await dialog.Result;
        if (result is { Canceled: false })
        {
            // Dialog chỉ trả thành công sau khi service lưu mapping/tag; lúc đó tải lại bảng từ SQL.
            await ReloadAsync();
        }
    }

    private async Task ToggleAsync(PlcDeviceMappingItem item)
    {
        // Luôn đọc lại cấu hình hiện tại theo Id để lấy tag và RowVersion mới nhất trước khi toggle.
        try
        {
            PlcDeviceConfiguration current = await Plc.ExecuteAsync(new GetPlcDeviceConfigurationQuery(item.Id), CancellationToken.None);
            PlcDeviceConfigurationRequest request = CreateToggleRequest(current);
            await Plc.ExecuteAsync(new UpdatePlcDeviceConfigurationCommand(item.Id, request), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể đổi trạng thái mapping; dữ liệu có thể đã thay đổi.";
        }
    }

    private static PlcDeviceConfigurationRequest CreateToggleRequest(PlcDeviceConfiguration current)
    {
        // Giữ nguyên mọi trường/tag và chỉ đảo IsActive; không reset counter checkpoint ngoài ý muốn.
        PlcDeviceMappingItem mapping = current.Mapping;
        return new PlcDeviceConfigurationRequest(
            mapping.ConnectionId,
            mapping.WorkUnitId,
            mapping.DefaultProductId,
            mapping.ExternalDeviceId,
            mapping.CounterMode,
            mapping.FaultCodeFormat,
            mapping.ZeroFaultCodeMeansNoFault,
            mapping.DebounceSamples,
            mapping.Note,
            !mapping.IsActive,
            current.Tags,
            mapping.RowVersion);
    }

    private async Task DeleteAsync(PlcDeviceMappingItem item)
    {
        // Đây là soft delete cấu hình; runtime/event lịch sử được giữ cho RCA và audit.
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { component => component.Title, "Xóa mapping PLC" },
            { component => component.Message, $"Soft-delete mapping {item.ExternalDeviceId}? Runtime/event lịch sử được giữ nguyên." },
            { component => component.ConfirmText, "Xóa" }
        };
        IDialogReference dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận", parameters);
        DialogResult? result = await dialog.Result;
        if (result is null || result.Canceled)
        {
            return;
        }

        try
        {
            await Plc.ExecuteAsync(new DeletePlcDeviceConfigurationCommand(item.Id, item.RowVersion), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể xóa mapping; dữ liệu có thể đã thay đổi.";
        }
    }

    private static string TagsTooltip(PlcDeviceMappingItem mapping) =>
        $"Đầu vào: {mapping.ReadTagCount}/5 tag · Đầu ra: {mapping.WriteTagCount}/9 tag. Bấm để cấu hình và đọc thử tag.";

    private static string CounterModeLabel(string mode) => mode switch
    {
        "Cumulative" => "Tích lũy", "ResetPerShift" => "Reset theo ca", "ResetDaily" => "Reset theo ngày", _ => mode
    };

    private static string FaultFormatLabel(string format) => format switch
    {
        "Auto" => "Tự động", "NumericDecimal" => "Thập phân", "Hex" => "Hex", "Text" => "Chuỗi", _ => format
    };

    private static string FormatTimestamp(DateTimeOffset? value)
    {
        // Timestamp I/O giữ offset; việc ToLocalTime ở đây chỉ phục vụ hiển thị.
        return value?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture) ?? "—";
    }

    private static SemanticKind ConnectivityKind(string value)
    {
        // Ánh xạ màu cho ConnectivityState, không trộn với OperationalState Running/Stopped.
        return value switch
        {
            "Connected" => SemanticKind.Success,
            "Disconnected" or "NoData" => SemanticKind.Warning,
            "BadTag" => SemanticKind.Error,
            _ => SemanticKind.Neutral
        };
    }
}
