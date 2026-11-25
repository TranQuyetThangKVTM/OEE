using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Web.Components.Shared;

namespace Oee.Web.ComponentTests;

/// <summary>Mở menu thật và chọn thao tác trong popover, thay cho bấm icon trực tiếp.</summary>
public abstract class RowMenuTestContext : BunitContext
{
    private IRenderedComponent<MudPopoverProvider>? menuProvider;
    protected IRenderedComponent<MudPopoverProvider> Menus => menuProvider ??= Render<MudPopoverProvider>();

    protected void OpenActions<T>(IRenderedComponent<T> page, int index = 0, string? menuLabel = null) where T : class, IComponent
    {
        _ = Menus;
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindComponents<RowActionMenu>()));
        var menu = menuLabel is null
            ? page.FindComponents<RowActionMenu>()[index]
            : page.FindComponents<RowActionMenu>().Single(item => item.Instance.Label == menuLabel);
        menu.Find("button").Click();
    }

    protected IElement MenuAction(string label) => Menus.FindAll("[aria-label]")
        .Single(element => element.GetAttribute("aria-label") == label);

    protected void SelectAction<T>(IRenderedComponent<T> page, string label, int index = 0, string? menuLabel = null) where T : class, IComponent
    {
        OpenActions(page, index, menuLabel);
        Menus.WaitForAssertion(() => Assert.Contains(Menus.FindAll("[aria-label]"), element => element.GetAttribute("aria-label") == label));
        MenuAction(label).Click();
    }
}
