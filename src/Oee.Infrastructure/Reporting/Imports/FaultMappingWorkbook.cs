using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using Oee.Application.Reporting;

namespace Oee.Infrastructure.Reporting;

/// <summary>Đọc/ghi XLSX và kiểm tra an toàn file; không quyết định hoặc ghi mapping nghiệp vụ.</summary>
public sealed class FaultMappingWorkbook(IOptions<ReportingOptions> options) : IFaultMappingWorkbook
{
    private const string Version = FaultMappingWorkbookData.TemplateVersion;
    /// <summary>Tạo workbook template versioned trong bộ nhớ.</summary>
    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("FaultMappings");
        sheet.Cell("A1").Value = Version; sheet.Cell("A1").Style.Font.Bold = true;
        var headers = new[] { "WorkUnitCode", "FaultCode", "ReasonCode", "Description", "IsActive" };
        for (var i = 0; i < headers.Length; i++) { sheet.Cell(3, i + 1).Value = headers[i]; sheet.Cell(3, i + 1).Style.Font.Bold = true; }
        sheet.Cell("G1").Value = "Không dùng công thức/macro. Để trống WorkUnitCode cho mapping global.";
        sheet.Column(1).Width = 20; sheet.Column(2).Width = 18; sheet.Column(3).Width = 20; sheet.Column(4).Width = 45; sheet.Column(5).Width = 12;
        using var output = new MemoryStream(); workbook.SaveAs(output); return output.ToArray();
    }
    public async Task<FaultMappingWorkbookData> ReadAsync(Stream source, long length, CancellationToken ct)
    {
        var bytes = await ReadSafeAsync(source, length, ct);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        if (workbook.Worksheets.Count != 1) return Invalid("Workbook", "SheetCount", "Template chỉ được có một worksheet.");
        var sheet = workbook.Worksheet(1);
        if (sheet.CellsUsed().Any(cell => cell.HasFormula)) return Invalid("Workbook", "Formula", "Không chấp nhận formula.");
        var versioned = sheet.Cell("A1").GetString() == Version;
        var header = versioned ? sheet.Row(3) : sheet.RowsUsed().FirstOrDefault();
        if (header is null) return Invalid("Workbook", "Header", "File Excel rỗng.");
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed()) columns[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        var byId = columns.ContainsKey("DowntimeReasonId");
        if (!columns.TryGetValue("FaultCode", out var faultColumn)
            || !columns.TryGetValue(byId ? "DowntimeReasonId" : "ReasonCode", out var reasonColumn))
            return Invalid("Workbook", "Header", "Cần cột FaultCode và ReasonCode hoặc DowntimeReasonId.");
        columns.TryGetValue(byId ? "WorkUnitId" : "WorkUnitCode", out var unitColumn);
        columns.TryGetValue("Description", out var descriptionColumn);
        columns.TryGetValue("IsActive", out var activeColumn);
        columns.TryGetValue("IsDeleted", out var deletedColumn);
        var last = sheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();
        var maxRows = Math.Min(options.Value.MaxImportRows, 10_000);
        if (last - header.RowNumber() > maxRows) return Invalid("Workbook", "RowLimit", $"Vượt giới hạn {maxRows} dòng.");
        var rows = new List<FaultMappingWorkbookRow>();
        for (var index = header.RowNumber() + 1; index <= last; index++)
        {
            ct.ThrowIfCancellationRequested();
            var row = sheet.Row(index);
            if (deletedColumn > 0 && BooleanText(row.Cell(deletedColumn)) is "true" or "1" or "yes") continue;
            rows.Add(new(index, Text(unitColumn), FaultText(row.Cell(faultColumn)), Text(reasonColumn),
                Text(descriptionColumn), activeColumn == 0 ? "true" : BooleanText(row.Cell(activeColumn))));
            string Text(int column) => column > 0 ? row.Cell(column).GetString() : string.Empty;
        }
        return new(last - header.RowNumber(), rows, []);
    }

    // Excel thường lưu 30000008 thành số hoặc scientific notation; không làm tròn mã dạng chuỗi.
    private static string FaultText(IXLCell cell)
    {
        if (cell.DataType != XLDataType.Number) return cell.GetString().Trim();
        var value = cell.GetDouble();
        return double.IsFinite(value) && value == Math.Truncate(value)
            ? value.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BooleanText(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        if (cell.DataType == XLDataType.Boolean) return cell.GetBoolean() ? "true" : "false";
        if (cell.DataType == XLDataType.Number) return cell.GetDouble() == 0 ? "false" : "true";
        return cell.GetString().Trim().ToLowerInvariant();
    }
    /// <summary>Kiểm tra kích thước, macro, expanded bytes và compression ratio trước ClosedXML parse.</summary>
    private async Task<byte[]> ReadSafeAsync(Stream source, long length, CancellationToken ct)
    {
        var max = Math.Min(options.Value.MaxImportBytes, 20_000_000); if (length <= 0 || length > max) throw new ArgumentException($"File phải từ 1 đến {max} bytes.");
        using var memory = new MemoryStream(); await source.CopyToAsync(memory, ct); if (memory.Length != length || memory.Length > max) throw new ArgumentException("Kích thước file không hợp lệ."); var bytes = memory.ToArray();
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read); long expanded = 0; foreach (var entry in zip.Entries) { if (entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Không chấp nhận macro/VBA."); expanded += entry.Length; if (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > 100) throw new InvalidDataException("Tỷ lệ nén không an toàn."); if (expanded > 50_000_000) throw new InvalidDataException("Workbook giải nén vượt giới hạn."); }
        return bytes;
    }
    private static FaultMappingWorkbookData Invalid(string field, string code, string message) => new(0, [], [new(0, field, code, message)]);
}
