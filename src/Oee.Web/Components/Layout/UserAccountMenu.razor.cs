using System.Security.Claims;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Oee.Application.Security;
using Oee.Web.Components.Presentation;

namespace Oee.Web.Components.Layout;

public partial class UserAccountMenu
{
    // Reference tới MudMenu cho phép cùng một trigger hỗ trợ cả click chuột và Enter/Space.
    private MudMenu? _userMenu;
    private Task OpenUserMenuAsync(MouseEventArgs args)
    {
        return _userMenu?.OpenMenuAsync(args) ?? Task.CompletedTask;
    }

    private Task OpenUserMenuFromKeyboardAsync(KeyboardEventArgs args)
    {
        return args.Key is "Enter" or " " ? _userMenu?.OpenMenuAsync(args) ?? Task.CompletedTask : Task.CompletedTask;
    }

    private static string UserName(ClaimsPrincipal user)
    {
        return user.Identity?.Name ?? "user";
    }

    private static string DisplayName(ClaimsPrincipal user)
    {
        return user.FindFirst("display_name")?.Value ?? UserName(user);
    }

    private static string Email(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value ?? "—";
    }

    private static IEnumerable<string> Roles(ClaimsPrincipal user)
    {
        // Role lấy từ signed authentication claims, không lấy từ input client tùy ý.
        return user.FindAll(ClaimTypes.Role).Select(claim => claim.Value);
    }

    private static SemanticKind RoleKind(string role)
    {
        // Chỉ ánh xạ màu hiển thị; authorization thật dùng capability policy phía server.
        return role switch
        {
            RoleCapabilityMatrix.Admin => SemanticKind.Error,
            RoleCapabilityMatrix.PlanningManager => SemanticKind.Warning,
            RoleCapabilityMatrix.Operator => SemanticKind.Info,
            _ => SemanticKind.Neutral
        };
    }

    private static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "OEE";
        }

        return string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(2).Select(part => char.ToUpperInvariant(part[0])));
    }
}
