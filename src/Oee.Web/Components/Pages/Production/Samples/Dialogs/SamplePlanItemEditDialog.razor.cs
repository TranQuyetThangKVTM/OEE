using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Update;

namespace Oee.Web.Components.Pages.Production;

public partial class SamplePlanItemEditDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public SamplePlanItemDetail Item { get; set; } = default!;

    [Parameter, EditorRequired]
    public ProductionLookups Lookups { get; set; } = default!;

    private MudForm? form;
    private Guid? workUnitId;
    private Guid? productId;
    private Guid? shiftId;
    private decimal quantity;
    private string? notes;
    private string? error;
    private bool saving;
    private IEnumerable<ProductionLookupOption> AvailableShifts => Lookups.Shifts.Where(x => x.SiteId == Lookups.WorkUnits.FirstOrDefault(unit => unit.Id == workUnitId)?.SiteId);

    protected override void OnInitialized()
    {
        workUnitId = Item.WorkUnitId;
        productId = Item.ProductId;
        shiftId = Item.ShiftId;
        quantity = Item.PlannedQuantity;
        notes = Item.Notes;
    }

    private void WorkUnitChanged(Guid? value)
    {
        workUnitId = value;
        if (!AvailableShifts.Any(x => x.Id == shiftId))
            shiftId = null;
    }

    private async Task SaveAsync()
    {
        if (saving)
            return;
        await (form?.ValidateAsync() ?? Task.CompletedTask);
        if (form?.IsValid != true)
            return;
        if (!workUnitId.HasValue || !productId.HasValue || !shiftId.HasValue || quantity <= 0)
        {
            error = "Vui lòng chọn thiết bị, sản phẩm, ca và sản lượng lớn hơn 0.";
            return;
        }

        saving = true;
        error = null;
        try
        {
            var request = new SamplePlanItemRequest(workUnitId.Value, productId.Value, shiftId.Value, quantity, notes);
            var saved = await Commands.ExecuteAsync(new UpdateSamplePlanItemCommand(Item.Id, request, Convert.FromBase64String(Item.RowVersion)), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(saved));
        }
        catch (Oee.Application.Common.ConcurrencyConflictException)
        {
            error = "Dòng mẫu đã thay đổi. Hãy đóng popup, làm mới và sửa lại.";
        }
        catch (Oee.Application.Common.PersistenceWriteException)
        {
            error = "Tổ hợp thiết bị, sản phẩm và ca đã có trong mẫu.";
        }
        catch (Exception)
        {
            error = "Không thể lưu dòng mẫu. Hãy kiểm tra dữ liệu hoặc thử lại.";
        }
        finally
        {
            saving = false;
        }
    }

    private static string ShiftText(ProductionLookupOption option) => option.StartTime.HasValue && option.EndTime.HasValue ? $"{option.Code} — {option.Name} ({option.StartTime:HH:mm}–{option.EndTime:HH:mm})" : $"{option.Code} — {option.Name}";
    private void Cancel() => Dialog.Cancel();
}
