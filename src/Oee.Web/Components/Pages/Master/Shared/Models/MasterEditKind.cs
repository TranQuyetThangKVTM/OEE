namespace Oee.Web.Components.Pages.Master;

/// <summary>Chọn loại master data cho dialog update/status dùng chung.</summary>
/// <remarks>WorkUnitStandard có workflow version/end-effectiveness riêng nên không nằm trong enum update chung.</remarks>
public enum MasterEditKind
{
    Product,
    Shift,
    DowntimeReason,
    WorkUnit,
    WorkCenter,
    Area,
    Enterprise,
    Site,
}
