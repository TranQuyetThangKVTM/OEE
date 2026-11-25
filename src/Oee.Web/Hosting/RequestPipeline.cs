using System.Globalization;

namespace Oee.Web.Hosting;
internal static class RequestPipeline
{
    public static void UseRequestPipeline(WebApplication app)
    {
        // Thứ tự middleware quan trọng: exception/status/HTTPS/localization trước auth, rate limit và antiforgery.
        app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseStatusCodePages();
        app.UseHttpsRedirection();
        // Cố định tiếng Việt; query string, cookie và Accept-Language không đổi ngôn ngữ ứng dụng.
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture),
            SupportedCultures = [culture],
            SupportedUICultures = [culture],
            RequestCultureProviders = [],
            ApplyCurrentCultureToResponseHeaders = true,
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseAntiforgery();
    }
}
