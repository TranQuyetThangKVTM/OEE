using MudBlazor;

namespace Oee.Web.Theming;

/// <summary>Design token và MudTheme tập trung cho giao diện công nghiệp OEE.</summary>
/// <remarks>Page/component nên dùng Color/MudTheme thay vì hard-code lại các mã màu này.</remarks>
public static class OeeTheme
{
    // X Automation: navy làm nền nhận diện, blue cho thao tác và cyan làm điểm nhấn.
    public const string Navy900 = "#061426";
    public const string Navy800 = "#0B2544";
    public const string Blue600 = "#075EC8";
    public const string Blue700 = "#064BA0";
    public const string Cyan400 = "#00D4EE";
    public const string Success500 = "#0B9F75";
    public const string Warning500 = "#F4B400";
    public const string Error500 = "#E42D32";
    public const string Info500 = "#2477E8";
    public const string Canvas = "#F3F7FC";
    public const string Surface = "#FFFFFF";
    public const string TextPrimary = "#14243B";
    public const string TextSecondary = "#475569";
    public const string Radius = "10px";

    /// <summary>Theme MudBlazor dùng chung cho LoginLayout và MainLayout.</summary>
    public static MudTheme Industrial { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Blue600,
            PrimaryDarken = Blue700,
            PrimaryContrastText = Surface,
            Secondary = Cyan400,
            SecondaryContrastText = Navy900,
            Success = Success500,
            Warning = Warning500,
            WarningContrastText = TextPrimary,
            Error = Error500,
            Info = Info500,
            Background = Canvas,
            BackgroundGray = "#EDF3FA",
            Surface = Surface,
            AppbarBackground = Navy900,
            AppbarText = Surface,
            DrawerBackground = Navy900,
            DrawerText = "#C9DDF1",
            TextPrimary = TextPrimary,
            TextSecondary = TextSecondary
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = Radius },
        Typography = new Typography { Default = new DefaultTypography { FontFamily = ["Inter", "Segoe UI", "Arial", "sans-serif"] }, H1 = new H1Typography { FontWeight = "700" }, H2 = new H2Typography { FontWeight = "700" }, H3 = new H3Typography { FontWeight = "700" }, H4 = new H4Typography { FontWeight = "700" }, H5 = new H5Typography { FontWeight = "700" }, H6 = new H6Typography { FontWeight = "700" } },
    };
}
