using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

public partial class AuthorizeAction
{
    /// <summary>Capability policy bắt buộc để render action.</summary>
    [Parameter, EditorRequired]
    public string Policy { get; set; } = string.Empty;

    /// <summary>Nội dung nút/icon được render khi actor có quyền.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
