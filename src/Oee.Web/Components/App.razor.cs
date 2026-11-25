using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace Oee.Web.Components;

public partial class App
{
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private static readonly IComponentRenderMode InteractiveWithoutPrerender = new InteractiveServerRenderMode(prerender: false);
    // Các trang nghiệp vụ đã đăng nhập tải SQL một lần trong phiên interactive.
    // Chưa đăng nhập chỉ render HTML/form HTTP, không phát descriptor tạo circuit ẩn danh.
    // Trang tài khoản đã đăng nhập vẫn prerender form HTTP/antiforgery trước khi tương tác.
    private IComponentRenderMode? PageRenderMode => HttpContext?.User.Identity?.IsAuthenticated != true ? null
        : HttpContext.Request.Path.StartsWithSegments("/Account") ? InteractiveServer : InteractiveWithoutPrerender;
}
