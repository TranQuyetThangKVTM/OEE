
namespace Oee.Web.Components.Pages;

internal sealed class UserEditorForm
{
    // Model binding nội bộ; không phải Identity entity và không có PasswordHash/SecurityStamp.
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? ConcurrencyToken { get; set; }
    public HashSet<string> Roles { get; set; } = [];
}
