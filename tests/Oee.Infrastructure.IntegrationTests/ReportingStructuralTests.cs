using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oee.Application.Contracts.Reporting;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.Reporting;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Kiểm tra PDF tiếng Việt, phân trang và template/import XLSX fault mapping.
/// </summary>
public sealed class ReportingStructuralTests
{
    private static readonly string[] ExpectedHeaders = ["WorkUnitCode", "FaultCode", "ReasonCode", "Description", "IsActive"];
    private static readonly DateTimeOffset FixedTime = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static OeeDbContext CreateDb() => new(new DbContextOptionsBuilder<OeeDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=OeeReportingStructural;Trusted_Connection=True;Encrypt=False")
        .Options);

    [Fact]
    public void RendererCreatesEmptyVietnameseReportWithoutLicenseConfiguration()
    {
        var service = new ReportExportService();
        var data = new ReportData(ReportKind.ShiftKpi, "KPI theo ca", "", "UTC", FixedTime,
            [], [], [], [], 0, false);

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(service.RenderPdf(data));
        var page = Assert.Single(pdf.GetPages());
        Assert.Contains("KPI theo ca", ContentOrderTextExtractor.GetText(page), StringComparison.Ordinal);
        Assert.Contains("Không có dữ liệu phù hợp", ContentOrderTextExtractor.GetText(page), StringComparison.Ordinal);
    }

    [Fact]
    public void RendererCreatesValidPdfForAllSupportedReportKinds()
    {
        var service = new ReportExportService();
        // Act & Assert: mọi loại đều tạo header %PDF và marker kết thúc file.
        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            var data = new ReportData(kind, kind.ToString(),
                "01/01/2026 - 02/01/2026", "UTC", FixedTime,
                [new("oee", "OEE", .85m, "%", true)], [], [],
                [new ReportTableRow(null, "M-001 · Ca 1", WorkUnit: "M-001", Oee: .85m,
                    Availability: .9m, Performance: .95m, Quality: .99m, Total: 1000,
                    Good: 990, Defect: 10, DowntimeMinutes: 12)], 1, true);
            var pdf = service.RenderPdf(data);
            Assert.True(pdf.Length > 5_000);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
            Assert.Contains("%%EOF", System.Text.Encoding.ASCII.GetString(pdf, Math.Max(0, pdf.Length - 32), Math.Min(32, pdf.Length)));
        }
    }

    [Fact]
    public void RendererPreservesVietnameseAndAllRowsAcrossPages()
    {
        var rows = Enumerable.Range(1, 120).Select(index => new ReportTableRow(null,
            $"Dòng {index:000} - Ca sáng", WorkUnit: "M01 - Máy thổi chai", Total: index,
            Good: index, Availability: 1, Performance: .978m, Quality: 1, Oee: .978m)).ToArray();
        var data = new ReportData(ReportKind.ShiftKpi, "KPI theo ca", "Nhà máy - Khu vực sản xuất",
            "Asia/Ho_Chi_Minh", FixedTime, [], [], [], rows, rows.Length, true);

        var bytes = new ReportExportService().RenderPdf(data);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var pages = pdf.GetPages().ToArray();
        var text = string.Join("\n", pages.Select(page => ContentOrderTextExtractor.GetText(page)));

        Assert.True(pages.Length > 1);
        foreach (var index in Enumerable.Range(1, 120))
            Assert.Contains($"Dòng {index:000}", text, StringComparison.Ordinal);
        foreach (var page in pages)
        {
            Assert.Contains("Kỳ / phạm vi", ContentOrderTextExtractor.GetText(page), StringComparison.Ordinal);
            Assert.Contains("Máy thổi chai", ContentOrderTextExtractor.GetText(page), StringComparison.Ordinal);
        }
        Assert.Contains("/FontFile2", System.Text.Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RendererSupportsConcurrentExportsWithCharts()
    {
        var data = new ReportData(ReportKind.DailyOee, "KPI ngày", "", "UTC", FixedTime,
            [new("oee", "OEE", .978m, "%", true)],
            [new("Ngày 01", 0, 1, 0, 1, 0, 0, 0, 0), new("Ngày 02", 1, 1, 1, 1, 100, 100, 0, 0)],
            [], [new(null, "M01", WorkUnit: "Máy thổi chai", Good: 100)], 1, true);
        var files = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => new ReportExportService().RenderPdf(data))));
        foreach (var bytes in files)
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
            var text = string.Join("\n", pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
            Assert.Contains("Xu hướng tương đối", text, StringComparison.Ordinal);
            Assert.Contains("100,0", text, StringComparison.Ordinal);
            Assert.Contains("0,0", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TemplateIsVersionedAndContainsExpectedColumns()
    {
        // Arrange & Act: tạo template XLSX rồi mở lại bằng ClosedXML.
        using var db = CreateDb(); var service = CreateImporter(db); var bytes = service.CreateTemplate();
        using var workbook = new XLWorkbook(new MemoryStream(bytes)); var sheet = workbook.Worksheet(1);
        // Assert: version và header cố định giúp importer phát hiện template sai phiên bản.
        Assert.Equal("OEE-FAULT-MAPPING-1.0", sheet.Cell("A1").GetString());
        Assert.Equal(ExpectedHeaders,
            Enumerable.Range(1, 5).Select(column => sheet.Cell(3, column).GetString()).ToArray());
    }

    [Fact]
    public async Task PreviewRejectsFormulaBeforeDatabaseAccess()
    {
        // Arrange: tạo workbook có formula; import nghiệp vụ chỉ cho dữ liệu thuần, không thực thi công thức.
        using var db = CreateDb(); var service = CreateImporter(db); using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("FaultMappings");
        sheet.Cell("A1").Value = "OEE-FAULT-MAPPING-1.0"; sheet.Cell("A4").FormulaA1 = "=1+1";
        using var stream = new MemoryStream(); workbook.SaveAs(stream); stream.Position = 0;
        // Act & Assert: lỗi được phát hiện ở bước parse trước khi cần truy vấn database.
        var preview = await service.PreviewAsync(stream, stream.Length, CancellationToken.None);
        Assert.Contains(preview.Issues, issue => issue.Code == "Formula");
    }

    [Fact]
    public async Task PreviewRejectsOversizedFileBeforeParsing()
    {
        // Arrange, Act & Assert: giới hạn byte được kiểm tra trước khi giải nén XLSX để giảm rủi ro cạn tài nguyên.
        using var db = CreateDb(); var service = CreateImporter(db); using var stream = new MemoryStream(new byte[32]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(stream, 5_000_001, CancellationToken.None));
    }

    private static Oee.Tests.UseCases.FaultMappingImportScenario CreateImporter(OeeDbContext db)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new FaultMappingWorkbook(Options.Create(new ReportingOptions
        { MaxImportBytes = 5_000_000, MaxImportRows = 100 })), new EfFaultMappingImportStore(unit), unit);
    }

}
