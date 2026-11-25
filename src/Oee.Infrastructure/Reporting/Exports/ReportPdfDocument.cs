using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Oee.Application.Contracts.Reporting;

namespace Oee.Infrastructure.Reporting;

/// <summary>Bố cục A4 ngang, lặp tiêu đề bảng và chân trang khi dữ liệu kéo dài nhiều trang.</summary>
internal static class ReportPdfDocument
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Color Navy = Color.FromRgb(11, 37, 68);
    private static readonly Color Blue = Color.FromRgb(7, 94, 200);
    private static readonly Color LightBlue = Color.FromRgb(235, 243, 251);
    private static readonly Color Border = Color.FromRgb(217, 226, 236);
    private const double ContentWidth = 27.7;

    internal static Document Create(ReportData data)
    {
        var document = new Document();
        document.Info.Title = data.Title;
        document.Info.Author = "OEE Management";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = ReportFontResolver.FamilyName;
        normal.Font.Size = 8;
        normal.Font.Color = Navy;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1);
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.7);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(.5);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(.5);

        var header = section.Headers.Primary.AddParagraph("OEE  |  " + data.Title);
        header.Format.Font.Size = 15;
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Blue;
        var footer = section.Footers.Primary.AddParagraph("OEE Management  -  Trang ");
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 7;
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        section.AddParagraph(data.FilterSummary);
        var localTime = TimeZoneInfo.ConvertTime(data.GeneratedAt, ReportTimeResolver.FindZone(data.TimeZoneId));
        var generated = section.AddParagraph($"Tạo lúc {localTime:dd/MM/yyyy HH:mm:ss}  |  {data.TimeZoneId}");
        generated.Format.Font.Color = Colors.Gray;
        generated.Format.SpaceAfter = Unit.FromPoint(8);
        AddMetrics(section, data.Metrics);

        if (data.Rows.Count == 0)
        {
            var empty = section.AddParagraph("Không có dữ liệu phù hợp với bộ lọc.");
            empty.Format.SpaceBefore = Unit.FromPoint(20);
            empty.Format.Alignment = ParagraphAlignment.Center;
        }
        else AddRows(section, data);

        AddBars(section, data);
        return document;
    }

    private static void AddMetrics(Section section, IReadOnlyCollection<ReportMetric> metrics)
    {
        foreach (var group in metrics.Chunk(6))
        {
            var table = NewTable(section, Enumerable.Repeat(1d, group.Length).ToArray());
            table.Borders.Width = .5;
            table.Borders.Color = Border;
            var row = table.AddRow();
            row.Shading.Color = LightBlue;
            for (var index = 0; index < group.Length; index++)
            {
                var metric = group[index];
                row[index].AddParagraph(metric.Label).Format.Font.Size = 7;
                var value = row[index].AddParagraph(metric.IsRatio ? Percent(metric.Value)
                    : $"{Number(metric.Value)} {metric.Unit}".Trim());
                value.Format.Font.Size = 12;
                value.Format.Font.Bold = true;
                value.Format.Font.Color = Blue;
            }
            section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(2);
        }
    }

    private static void AddRows(Section section, ReportData data)
    {
        var headings = Headers(data.Kind);
        var table = NewTable(section, Weights(data.Kind));
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Navy;
        header.Format.Font.Color = Colors.White;
        header.Format.Font.Bold = true;
        for (var index = 0; index < headings.Length; index++) header[index].AddParagraph(headings[index]);

        var rowNumber = 0;
        foreach (var item in data.Rows)
        {
            var row = table.AddRow();
            row.Borders.Bottom.Width = .4;
            row.Borders.Bottom.Color = Border;
            if (rowNumber++ % 2 == 0) row.Shading.Color = LightBlue;
            var cells = Cells(data.Kind, item, data.TimeZoneId);
            for (var index = 0; index < cells.Length; index++)
            {
                row[index].AddParagraph(cells[index]);
                if (IsNumeric(data.Kind, index)) row[index].Format.Alignment = ParagraphAlignment.Right;
            }
        }
    }

    private static Table NewTable(Section section, double[] weights)
    {
        var table = section.AddTable();
        table.Rows.LeftIndent = Unit.Zero;
        table.TopPadding = Unit.FromPoint(5);
        table.BottomPadding = Unit.FromPoint(5);
        table.LeftPadding = Unit.FromPoint(4);
        table.RightPadding = Unit.FromPoint(4);
        var total = weights.Sum();
        foreach (var weight in weights) table.AddColumn(Unit.FromCentimeter(ContentWidth * weight / total));
        return table;
    }

    private static void AddBars(Section section, ReportData data)
    {
        var bars = data.Breakdown.Count > 0
            ? data.Breakdown.Take(10).Select(x => (x.Label, Value: x.Percentage)).ToArray()
            : data.Trend.TakeLast(10).Select(x => (x.Label, Value: x.Oee ?? x.Quality ?? 0)).ToArray();
        if (bars.Length == 0) return;
        var title = section.AddParagraph(data.Breakdown.Count > 0 ? "Phân bố tổn thất" : "Xu hướng tương đối");
        title.Format.SpaceBefore = Unit.FromPoint(12);
        title.Format.Font.Bold = true;
        title.Format.KeepWithNext = true;
        foreach (var bar in bars)
        {
            var ratio = (double)Math.Clamp(bar.Value, 0, 1);
            var table = NewTable(section, [3, 7, 1]);
            var row = table.AddRow();
            row[0].AddParagraph(bar.Label);
            var chart = row[1].Elements.AddTable();
            chart.Rows.LeftIndent = Unit.Zero;
            chart.AddColumn(Unit.FromCentimeter(16 * Math.Max(ratio, .00001)));
            chart.AddColumn(Unit.FromCentimeter(16 * Math.Max(1 - ratio, .00001)));
            var chartRow = chart.AddRow();
            chartRow.HeightRule = RowHeightRule.Exactly;
            chartRow.Height = Unit.FromPoint(7);
            chartRow[0].Shading.Color = ratio > 0 ? Blue : LightBlue;
            chartRow[1].Shading.Color = LightBlue;
            row[2].AddParagraph(Percent(bar.Value)).Format.Alignment = ParagraphAlignment.Right;
        }
    }

    private static string[] Headers(ReportKind kind) => kind switch
    {
        ReportKind.DowntimeEvents => ["Sự kiện", "Thiết bị", "Bắt đầu", "Kết thúc", "Phút", "Phân loại", "Nhóm", "Nguồn", "Trạng thái", "Ghi chú"],
        ReportKind.QualityLoss => ["SKU", "Mã", "Tổng", "Đạt", "Lỗi", "Quality", "Tỷ lệ lỗi"],
        ReportKind.Reliability => ["Thiết bị", "Sự cố", "Uptime (phút)", "Dừng (phút)", "MTBF (phút)", "MTTR (phút)", "Availability"],
        _ => ["Kỳ / phạm vi", "Thiết bị", "A", "P", "Q", "OEE", "Tổng", "Đạt", "Lỗi", "Loading (phút)"]
    };

    private static double[] Weights(ReportKind kind) => kind switch
    {
        ReportKind.DowntimeEvents => [1.3, 1.8, 1.6, 1.6, .8, 1.2, 1.2, .8, 1, 2.1],
        ReportKind.QualityLoss => [2.6, 1.3, 1.2, 1.2, 1, 1.1, 1.1],
        ReportKind.Reliability => [3.2, .8, 1.4, 1.2, 1.2, 1.2, 1.1],
        _ => [2.6, 2.2, .8, .8, .8, .8, 1.2, 1.2, 1, 1.2]
    };

    private static bool IsNumeric(ReportKind kind, int column) => kind switch
    {
        ReportKind.DowntimeEvents => column == 4,
        ReportKind.QualityLoss => column >= 2,
        ReportKind.Reliability => column >= 1,
        _ => column >= 2
    };

    private static string[] Cells(ReportKind kind, ReportTableRow row, string zone) => kind switch
    {
        ReportKind.DowntimeEvents => [row.Code ?? row.Label, row.WorkUnit ?? "—", DateTimeValue(row.PeriodStart, zone), DateTimeValue(row.PeriodEnd, zone), Number(row.DowntimeMinutes), row.Category ?? "—", row.Kind ?? "—", row.Source ?? "—", row.Status ?? "—", row.Note ?? "—"],
        ReportKind.QualityLoss => [row.Label, row.Code ?? "—", Number(row.Total), Number(row.Good), Number(row.Defect), Percent(row.Quality), Percent(row.Attainment)],
        ReportKind.Reliability => [(row.Code ?? "") + " · " + row.Label, row.EventCount.ToString(Culture), Number(row.UptimeMinutes), Number(row.DowntimeMinutes), Number(row.MtbfMinutes), Number(row.MttrMinutes), Percent(row.Availability)],
        _ => [row.Label, row.WorkUnit ?? "—", Percent(row.Availability), Percent(row.Performance), Percent(row.Quality), Percent(row.Oee), Number(row.Total), Number(row.Good), Number(row.Defect), Number(row.LoadingMinutes)]
    };

    private static string Percent(decimal? value) => value?.ToString("P1", Culture) ?? "—";
    private static string Number(decimal? value) => value?.ToString("#,0.####", Culture) ?? "—";
    private static string DateTimeValue(DateTimeOffset? value, string zone) => value.HasValue
        ? TimeZoneInfo.ConvertTime(value.Value, ReportTimeResolver.FindZone(zone)).ToString("dd/MM/yyyy HH:mm", Culture) : "—";
}
