using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Devices.Create;
using Oee.Application.PlcIntegration.Configuration.Devices.Queries;
using Oee.Application.PlcIntegration.Configuration.Devices.Update;

namespace Oee.Web.Components.Pages.Plc;

public partial class PlcMappingEditorDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public Guid? MappingId { get; set; }
    [Parameter, EditorRequired] public PlcConfigurationLookups Lookups { get; set; } = default!;
    private MudForm? _form;
    private PlcDeviceConfiguration? _existing;
    private string _externalId = string.Empty;
    private Guid _connectionId;
    private Guid _workUnitId;
    private Guid? _productId;
    private string? _note;
    private string? _error;
    private bool _saving;
    private bool _loading;

    protected override async Task OnInitializedAsync()
    {
        _connectionId = Lookups.Connections.FirstOrDefault()?.Id ?? Guid.Empty;
        if (!MappingId.HasValue) return;
        _loading = true;
        try
        {
            _existing = await Plc.ExecuteAsync(new GetPlcDeviceConfigurationQuery(MappingId.Value), CancellationToken.None);
            var mapping = _existing.Mapping;
            _externalId = mapping.ExternalDeviceId;
            _connectionId = mapping.ConnectionId;
            _workUnitId = mapping.WorkUnitId;
            _productId = mapping.DefaultProductId;
            _note = mapping.Note;
        }
        catch { _error = "Không thể tải ánh xạ. Hãy đóng và thử lại."; }
        finally { _loading = false; }
    }

    private Task<IEnumerable<Guid>> SearchWorkUnits(string text, CancellationToken cancellationToken) =>
        Task.FromResult(Lookups.WorkUnits.Where(item => Matches(item, text)).Select(item => item.Id));

    private Task<IEnumerable<Guid?>> SearchProducts(string text, CancellationToken cancellationToken) =>
        Task.FromResult(Lookups.Products.Where(item => Matches(item, text)).Select(item => (Guid?)item.Id));

    private static bool Matches(PlcLookup item, string text) => string.IsNullOrWhiteSpace(text)
        || (item.Code + " " + item.Name).Contains(text, StringComparison.OrdinalIgnoreCase);
    private string WorkUnitLabel(Guid id) => Label(Lookups.WorkUnits.FirstOrDefault(item => item.Id == id));
    private string ProductLabel(Guid? id) => Label(Lookups.Products.FirstOrDefault(item => item.Id == id));
    private static string Label(PlcLookup? item) => item is null ? string.Empty : $"{item.Code} — {item.Name}";

    private async Task SaveAsync()
    {
        if (_saving || _loading || (MappingId.HasValue && _existing is null)) return;
        await _form!.ValidateAsync();
        if (!_form.IsValid || _workUnitId == Guid.Empty || _connectionId == Guid.Empty)
        {
            _error = "Chọn kết nối và thiết bị OEE, nhập mã thiết bị PLC.";
            return;
        }
        _saving = true;
        try
        {
            var mapping = _existing?.Mapping;
            // Sửa thông tin không xóa tag, không reset counter, không đổi tùy chọn thu thập.
            var request = new PlcDeviceConfigurationRequest(_connectionId, _workUnitId, _productId, _externalId,
                mapping?.CounterMode ?? "Cumulative", mapping?.FaultCodeFormat ?? "Auto",
                mapping?.ZeroFaultCodeMeansNoFault ?? true, mapping?.DebounceSamples ?? 2, _note,
                mapping?.IsActive ?? true, _existing?.Tags ?? [], mapping?.RowVersion);
            if (MappingId.HasValue)
                await Plc.ExecuteAsync(new UpdatePlcDeviceConfigurationCommand(MappingId.Value, request), CancellationToken.None);
            else
                await Plc.ExecuteAsync(new CreatePlcDeviceConfigurationCommand(request), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(true));
        }
        catch { _error = "Không thể lưu. Kiểm tra mã trùng hoặc ánh xạ đã được người khác sửa."; }
        finally { _saving = false; }
    }

    private void Cancel() => Dialog.Cancel();
}
