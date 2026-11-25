using Bunit;
using MudBlazor.Services;
using Oee.Web.Components.Pages;

namespace Oee.Web.ComponentTests;

public sealed class AccessDeniedTests : BunitContext
{
    public AccessDeniedTests() => Services.AddMudServices();

    [Fact]
    public void PageExplainsAuthorizationFailureAndOffersSafeNavigation()
    {
        var component = Render<AccessDenied>();
        Assert.Contains("Không có quyền truy cập", component.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/\"", component.Markup, StringComparison.Ordinal);
    }
}
