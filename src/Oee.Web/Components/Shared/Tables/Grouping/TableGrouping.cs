using MudBlazor;

namespace Oee.Web.Components.Shared;

/// <summary>Giữ definition ổn định qua render/refresh để không tự mở lại nhóm đã thu gọn.</summary>
public sealed class TableGrouping<TItem>
{
    private readonly TableGroupDefinition<TItem> machines;
    private readonly TableGroupDefinition<TItem>? products;

    public TableGrouping(Func<TItem, TableGroupKey> machine, Func<TItem, TableGroupKey>? product = null,
        TableGroupingMode initial = TableGroupingMode.None)
    {
        machines = Create("Máy", machine);
        if (product is not null) products = Create("SKU", product);
        SetMode(initial);
    }

    public TableGroupingMode Mode { get; private set; }
    public TableGroupDefinition<TItem>? Definition => Mode switch
    {
        TableGroupingMode.Machine => machines,
        TableGroupingMode.Product => products,
        _ => null
    };

    public void SetMode(TableGroupingMode mode)
    {
        if (!Enum.IsDefined(mode) || mode == TableGroupingMode.Product && products is null)
            throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
    }

    private static TableGroupDefinition<TItem> Create(string name, Func<TItem, TableGroupKey> selector) => new()
    {
        GroupName = name, Indentation = false, Expandable = true, IsInitiallyExpanded = true,
        Selector = item => selector(item)
    };
}
