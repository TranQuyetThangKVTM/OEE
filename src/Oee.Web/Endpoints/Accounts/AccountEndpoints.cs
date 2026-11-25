using Microsoft.AspNetCore.Identity;
using Oee.Application.Common;
using Oee.Application.Identity.Users.ChangeOwnPassword;
using Oee.Infrastructure.Identity;

namespace Oee.Web.Endpoints;
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(WebApplication app)
    { // Form login validate antiforgery, dùng SignInManager để áp dụng password hash/lockout chuẩn của Identity.
        // Password chỉ sống trong request scope và không được ghi log/audit.
        app.MapPost("/account/sign-in", async (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, CancellationToken ct) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var form = await context.Request.ReadFormAsync(ct);
            var userName = form["userName"].ToString().Trim();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();
            var rememberMe = string.Equals(form["rememberMe"], "true", StringComparison.OrdinalIgnoreCase) || string.Equals(form["rememberMe"], "on", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
                return Results.LocalRedirect($"/Account/Login?error=required&returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}");
            var user = await userManager.FindByNameAsync(userName);
            if (user is null || user.IsDisabled)
                return Results.LocalRedirect($"/Account/Login?error=invalid&returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}");
            // lockoutOnFailure làm tăng AccessFailedCount và khóa user theo IdentitySecurityOptions.
            var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: rememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
                return Results.LocalRedirect(SafeReturnUrl(returnUrl));
            var error = result.IsLockedOut ? "locked" : "invalid";
            return Results.LocalRedirect($"/Account/Login?error={error}&returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}");
        }).AllowAnonymous();
        // Đổi mật khẩu cá nhân qua Application service, sau đó refresh cookie để cập nhật SecurityStamp/session.
        app.MapPost("/account/change-password", async (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, IUseCaseExecutor users, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, CancellationToken ct) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var form = await context.Request.ReadFormAsync(ct);
            var currentPassword = form["currentPassword"].ToString();
            var newPassword = form["newPassword"].ToString();
            var confirmPassword = form["confirmPassword"].ToString();
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                return Results.LocalRedirect("/Account/ChangePassword?error=required");
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return Results.LocalRedirect("/Account/ChangePassword?error=mismatch");
            }

            try
            {
                await users.ExecuteAsync(new ChangeOwnPasswordCommand(new(currentPassword, newPassword)), ct);
                var user = await userManager.GetUserAsync(context.User);
                if (user is not null)
                {
                    await signInManager.RefreshSignInAsync(user);
                }

                return Results.LocalRedirect("/Account/ChangePassword?success=true");
            }
            catch (Oee.Application.Common.ApplicationRuleException)
            {
                return Results.LocalRedirect("/Account/ChangePassword?error=invalid");
            }
        }).RequireAuthorization();
        // Logout cũng là mutation cookie nên bắt buộc antiforgery.
        app.MapPost("/account/logout", async (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            await signInManager.SignOutAsync();
            return Results.LocalRedirect("/");
        }).RequireAuthorization();
    }

    private // Chỉ cho redirect nội bộ dạng relative path để ngăn open-redirect ra domain do client chỉ định.
    static string SafeReturnUrl(string? returnUrl) => !string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) ? returnUrl : "/";
}
