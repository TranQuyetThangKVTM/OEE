using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Oee.Infrastructure.Identity;
using Oee.Web.Configuration;
using Oee.Web.Security;

namespace Oee.Web.Hosting;
internal static class IdentityRegistration
{
    public static IdentitySecurityOptions AddIdentityOptions(WebApplicationBuilder builder)
    {
        // Password/cookie/lockout policy đến từ cấu hình; UI không hard-code một policy khác server.
        var identitySecurity = builder.Configuration.GetSection(IdentitySecurityOptions.SectionName).Get<IdentitySecurityOptions>() ?? new IdentitySecurityOptions();
        builder.Services.AddOptions<IdentitySecurityOptions>().Bind(builder.Configuration.GetSection(IdentitySecurityOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = identitySecurity.RequiredLength;
            options.Password.RequireUppercase = identitySecurity.RequireUppercase;
            options.Password.RequireLowercase = identitySecurity.RequireLowercase;
            options.Password.RequireDigit = identitySecurity.RequireDigit;
            options.Password.RequireNonAlphanumeric = identitySecurity.RequireNonAlphanumeric;
            options.Lockout.MaxFailedAccessAttempts = identitySecurity.MaxFailedAccessAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identitySecurity.LockoutMinutes);
            options.Lockout.AllowedForNewUsers = true;
        });
        return identitySecurity;
    }

    public static void AddIdentityCookies(WebApplicationBuilder builder, IdentitySecurityOptions identitySecurity)
    { // Identity cookie là cơ chế authentication phase 1; authorization capability được đăng ký riêng bên dưới.
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            // Cookie không đọc được từ JavaScript; Production bắt buộc HTTPS và session trượt theo thời lượng cấu hình.
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/access-denied";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(identitySecurity.CookieHours);
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    // API/transport không chuyển sang HTML login; chặn trước khi tạo connection/circuit.
                    if (IsTransportOrApi(context.Request.Path))
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    else
                        context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = context =>
                {
                    if (IsTransportOrApi(context.Request.Path))
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    else
                        context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                },
                OnValidatePrincipal = async context =>
                {
                    // Mỗi lần principal được validate, user đã disable sẽ bị reject và sign-out khỏi session hiện tại.
                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.GetUserAsync(context.Principal!);
                    if (user is null || user.IsDisabled)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    }
                },
            };
        });
        builder.Services.AddCapabilityAuthorization();
    }

    private static bool IsTransportOrApi(PathString path) => path.StartsWithSegments("/api")
        || path.StartsWithSegments("/_blazor")
        || path.StartsWithSegments("/hubs");
}
