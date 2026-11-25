using MigraDoc;
using MigraDoc.Rendering;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;
using PdfSharp.Fonts;

namespace Oee.Infrastructure.Reporting;

/// <summary>Dựng PDF từ cùng ReportData của UI, không truy vấn hoặc tính lại số liệu.</summary>
public sealed class ReportExportService : IReportExportService
{
    // Đăng ký font một lần trước lượt render đầu tiên, độc lập Windows Fonts/quyền App Pool.
    private static readonly Lazy<bool> FontRegistration = new(() =>
    {
        GlobalFontSettings.FontResolver ??= new ReportFontResolver();
        PredefinedFontsAndChars.ErrorFontName = ReportFontResolver.FamilyName;
        PredefinedFontsAndChars.Bullets.Level1FontName = ReportFontResolver.FamilyName;
        PredefinedFontsAndChars.Bullets.Level2FontName = ReportFontResolver.FamilyName;
        PredefinedFontsAndChars.Bullets.Level3FontName = ReportFontResolver.FamilyName;
        return true;
    });

    public byte[] RenderPdf(ReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _ = FontRegistration.Value;
        var renderer = new PdfDocumentRenderer { Document = ReportPdfDocument.Create(data) };
        renderer.RenderDocument();
        using var pdf = renderer.PdfDocument;
        using var stream = new MemoryStream();
        pdf.Save(stream, false);
        return stream.ToArray();
    }
}
