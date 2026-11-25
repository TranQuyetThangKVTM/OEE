using Microsoft.AspNetCore.Antiforgery;

namespace Oee.Web.Security;

/// <summary>Endpoint filter xác thực antiforgery token cho mutation dùng cookie authentication.</summary>
/// <remarks>Authorization kiểm tra ai được phép; antiforgery kiểm tra request có thực sự xuất phát từ UI hợp lệ.</remarks>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    /// <summary>Chỉ gọi endpoint handler tiếp theo sau khi token CSRF được xác thực.</summary>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        await antiforgery.ValidateRequestAsync(context.HttpContext);
        return await next(context);
    }
}
