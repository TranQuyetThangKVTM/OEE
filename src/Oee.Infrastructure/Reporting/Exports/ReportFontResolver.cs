using PdfSharp.Fonts;

namespace Oee.Infrastructure.Reporting;

/// <summary>Font Noto Sans OFL nhúng cùng assembly, có đầy đủ dấu tiếng Việt.</summary>
internal sealed class ReportFontResolver : IFontResolver
{
    internal const string FamilyName = "Oee Noto Sans";
    private const string Regular = "NotoSans-Regular";
    private const string Bold = "NotoSans-Bold";
    private static readonly Lazy<byte[]> RegularBytes = new(() => ReadFont(Regular));
    private static readonly Lazy<byte[]> BoldBytes = new(() => ReadFont(Bold));

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase)
            ? new FontResolverInfo(bold ? Bold : Regular, false, italic)
            : null;

    public byte[] GetFont(string faceName) => faceName switch
    {
        Regular => RegularBytes.Value,
        Bold => BoldBytes.Value,
        _ => throw new InvalidOperationException("Không tìm thấy font báo cáo.")
    };

    private static byte[] ReadFont(string name)
    {
        var assembly = typeof(ReportFontResolver).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(resource => resource.EndsWith($".Fonts.{name}.ttf", StringComparison.Ordinal));
        using var source = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Thiếu font nhúng của báo cáo.");
        using var target = new MemoryStream();
        source.CopyTo(target);
        return target.ToArray();
    }
}
