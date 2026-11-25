using Oee.Application.Contracts.Reporting;
using Oee.Application.Contracts.Modules;
using static Oee.Application.Chat.ChatFormatting;

namespace Oee.Application.Chat;

internal sealed class ChatReportAnswers(ChatContext context)
{
    internal async Task<ChatAnswerDto> AnswerAsync(
        ChatIntent intent,
        ReportFilterOption? unit,
        ReportFilterOption? second,
        ReportFilterOptions filters,
        CancellationToken cancellationToken)
    {
        var slots = intent.Slots;
        var range = slots.Range;
        var scope = unit is null ? "toàn hệ thống" : $"{unit.Code} · {unit.Name}";
        var heading = $"{scope}, {range.From:dd/MM/yyyy}–{range.To:dd/MM/yyyy}";

        if (intent.Kind is ChatIntentKind.OeeSummary or ChatIntentKind.DashboardSummary or ChatIntentKind.ProductionTotal or ChatIntentKind.PeriodComparison or ChatIntentKind.WorkUnitDetail)
        {
            var current = await context.Dashboard.GetAsync(new(range.From, range.To, WorkUnitId: unit?.Id), cancellationToken);

            if (intent.Kind == ChatIntentKind.DashboardSummary && unit is null)
                return await SummarizeDashboardAsync(intent, current, heading, cancellationToken);

            if (!current.HasData)
                return Answer(intent.Kind, $"Chưa có snapshot hoặc sản lượng cho {heading}.", "Bảng điều khiển", "/");
            var text = $"Kết quả {heading} ({current.TimeZoneId}):\nOEE: {Percent(current.Oee.Value)}; A: {Percent(current.Availability.Value)}; P: {Percent(current.Performance.Value)}; Q: {Percent(current.Quality.Value)}.\nSản lượng: {Number(current.Production.Total)} tổng; {Number(current.Production.Good)} đạt; {Number(current.Production.Reject)} lỗi.";

            if (intent.Kind == ChatIntentKind.PeriodComparison)
            {
                var days = range.To.DayNumber - range.From.DayNumber + 1;
                var previousFrom = range.From.AddDays(-days);
                var previousTo = range.From.AddDays(-1);

                if (slots.Topic == "compare-week")
                    previousFrom = range.From.AddDays(-7);

                if (slots.Topic == "compare-month")
                    previousFrom = range.From.AddMonths(-1);
                var previous = await context.Dashboard.GetAsync(new(previousFrom, previousTo, WorkUnitId: unit?.Id), cancellationToken);
                text += previous.HasData
                    ? $"\nKỳ trước ({previousFrom:dd/MM}–{previousTo:dd/MM}): OEE {Percent(previous.Oee.Value)}. Chênh lệch {Percent(current.Oee.Value - previous.Oee.Value)} (điểm phần trăm)."
                    : "\nChưa có dữ liệu kỳ trước để so sánh.";
            }

            return Answer(intent.Kind, text, "Bảng điều khiển", "/");
        }

        if (intent.Kind == ChatIntentKind.CompareWorkUnits)
        {
            if (unit is null || second is null)
                return Answer(
                    intent.Kind,
                    "Hãy nhập hai mã máy, ví dụ: So sánh máy M01 với máy M03 tuần này.",
                    "KPI ngày",
                    "/reports/daily-oee");
            var a = await context.Dashboard.GetAsync(new(range.From, range.To, WorkUnitId: unit.Id), cancellationToken);
            var b = await context.Dashboard.GetAsync(new(range.From, range.To, WorkUnitId: second.Id), cancellationToken);

            return Answer(
                intent.Kind,
                $"So sánh {range.Label}:\n{unit.Code}: OEE {Percent(a.Oee.Value)}, sản lượng đạt {Number(a.Production.Good)}.\n{second.Code}: OEE {Percent(b.Oee.Value)}, sản lượng đạt {Number(b.Production.Good)}.\nHai máy dùng cùng khoảng {range.From:dd/MM/yyyy}–{range.To:dd/MM/yyyy}; giá trị chưa xác định không được coi là 0%.",
                "KPI ngày",
                "/reports/daily-oee");
        }

        if (intent.Kind is ChatIntentKind.WorstShift or ChatIntentKind.BestShift)
        {
            if (slots.Topic == "day-ranking")
            {
                var daily = await context.Reports.QueryAsync(new(ReportKind.DailyOee, range.From, range.To, WorkUnitId: unit?.Id), cancellationToken);
                var definedDays = daily.Trend.Where(x => x.Oee.HasValue);
                var orderedDays = intent.Kind == ChatIntentKind.BestShift
                    ? definedDays.OrderByDescending(x => x.Oee)
                    : definedDays.OrderBy(x => x.Oee);
                var rankedDays = orderedDays.Take(slots.TopN).Select(x => $"{x.Label}: OEE {Percent(x.Oee)}.").ToArray();

                return Answer(
                    intent.Kind,
                    $"OEE theo ngày ({heading}):\n" + (rankedDays.Length == 0 ? "Chưa có dữ liệu hợp lệ." : string.Join('\n', rankedDays)),
                    "KPI theo ngày",
                    "/reports/daily-oee");
            }
            var values = new List<(string Name, decimal Value)>();

            foreach (var shift in filters.Shifts)
            {
                var result = await context.Reports.QueryAsync(
                    new(ReportKind.ShiftKpi, range.From, range.To, WorkUnitId: unit?.Id, ShiftId: shift.Id),
                    cancellationToken);
                var oee = result.Metrics.FirstOrDefault(metric => metric.Key == "oee")?.Value;

                if (result.HasData && oee.HasValue)
                    values.Add(($"{shift.Code} · {shift.Name}", oee.Value));
            }
            var ordered = intent.Kind == ChatIntentKind.BestShift
                ? values.OrderByDescending(x => x.Value)
                : values.OrderBy(x => x.Value);

            return Answer(
                intent.Kind,
                values.Count == 0
                    ? $"Chưa có OEE theo ca cho {heading}."
                    : $"OEE theo ca ({heading}), tổng hợp từ số liệu tuyệt đối:\n" + string.Join('\n', ordered.Take(slots.TopN).Select(x => $"{x.Name}: {Percent(x.Value)}")),
                "KPI theo ca",
                "/reports/shift-kpi");
        }

        var kind = intent.Kind switch
        {
            ChatIntentKind.TopLossReasons or ChatIntentKind.WorkUnitLossReasons or ChatIntentKind.TopDowntimeMachines => ReportKind.DowntimeEvents,
            ChatIntentKind.MostUnreliableMachines => ReportKind.Reliability,
            ChatIntentKind.DefectiveProducts => ReportKind.QualityLoss,
            ChatIntentKind.OeeTimeline => ReportKind.DailyOee,
            _ => ReportKind.DeviceOee
        };
        var report = await context.Reports.QueryAsync(
            new(kind, range.From, range.To, WorkUnitId: unit?.Id, PageSize: 5000),
            cancellationToken);
        var link = kind switch
        {
            ReportKind.DowntimeEvents => "/reports/downtime-events",
            ReportKind.Reliability => "/reports/reliability",
            _ => "/reports/daily-oee"
        };

        if (!report.HasData)
            return Answer(intent.Kind, $"Chưa có dữ liệu cho {heading}.", "Mở báo cáo", link);

        if (report.TotalCount > report.Rows.Count)
            return Answer(
                intent.Kind,
                "Khoảng dữ liệu quá lớn để xếp hạng chính xác trong chat. Hãy chọn một máy hoặc khoảng ngày ngắn hơn.",
                "Mở báo cáo",
                link);

        IEnumerable<string> lines;

        switch (intent.Kind)
        {
            case ChatIntentKind.TopLossReasons:
            case ChatIntentKind.WorkUnitLossReasons:
                lines = report.Breakdown.OrderByDescending(x => x.Value).Take(slots.TopN)
                    .Select(x => $"{x.Label}: {x.Value:N1} phút; {x.EventCount} sự kiện.");
                break;
            case ChatIntentKind.TopDowntimeMachines:
                lines = report.Rows.GroupBy(x => x.WorkUnit).OrderByDescending(g => g.Sum(x => x.DowntimeMinutes)).Take(slots.TopN)
                    .Select(g => $"{g.Key}: {g.Sum(x => x.DowntimeMinutes):N1} phút; {g.Count()} sự kiện.");
                break;
            case ChatIntentKind.MostUnreliableMachines:
                lines = report.Rows.OrderByDescending(x => x.EventCount).ThenByDescending(x => x.DowntimeMinutes).Take(slots.TopN)
                    .Select(x => $"{x.WorkUnit ?? x.Label}: {x.EventCount} sự cố; MTBF {Number(x.MtbfMinutes)} phút; MTTR {Number(x.MttrMinutes)} phút.");
                break;
            case ChatIntentKind.DefectiveProducts:
                lines = report.Rows.Where(x => x.Total > 0).OrderByDescending(x => x.Defect / x.Total).Take(slots.TopN)
                    .Select(x => $"{x.Product ?? x.Label}: {Number(x.Defect)} lỗi / {Number(x.Total)} tổng; tỷ lệ lỗi {Percent(x.Defect / x.Total)}.");
                break;
            case ChatIntentKind.OeeTimeline:
                lines = report.Trend.TakeLast(31).Select(x => $"{x.Label}: OEE {Percent(x.Oee)}.");
                break;
            default:
                var defined = report.Rows.Where(x => x.Oee.HasValue);
                var ranked = intent.Kind == ChatIntentKind.TopPerformers
                    ? defined.OrderByDescending(x => x.Oee)
                    : defined.OrderBy(x => x.Oee);
                lines = ranked.Take(slots.TopN)
                    .Select(x => $"{x.WorkUnit ?? x.Label}: OEE {Percent(x.Oee)}; A {Percent(x.Availability)}; P {Percent(x.Performance)}; Q {Percent(x.Quality)}; đạt {Number(x.Good)}.");
                break;
        }
        var rows = lines.ToArray();

        return Answer(
            intent.Kind,
            $"{heading} ({report.TimeZoneId}):\n" + (rows.Length == 0 ? "Chưa có giá trị hợp lệ để xếp hạng." : string.Join('\n', rows)),
            "Mở báo cáo",
            link);
    }

    private async Task<ChatAnswerDto> SummarizeDashboardAsync(
        ChatIntent intent,
        DashboardModel dashboard,
        string heading,
        CancellationToken cancellationToken)
    {
        // Các query dùng chung scope; đọc tuần tự để không chạy song song trên cùng DbContext.
        var live = await context.Live.GetAsync(new(), cancellationToken);
        var open = await context.Alerts.ListAsync(new(State: "Open", PageSize: 1), cancellationToken);
        var critical = await context.Alerts.ListAsync(new(State: "Open", Severity: "Critical", PageSize: 1), cancellationToken);
        var confirmed = live.Machines.Where(x => live.StreamState == "Live" && x.ConnectivityState == "Connected" && !x.IsStale).ToArray();
        var running = confirmed.Count(x => x.OperationalState == "Running");
        var stopped = confirmed.Count(x => x.OperationalState == "Stopped");
        var unknown = live.Machines.Count - confirmed.Length;
        var text = $"**Tóm tắt {intent.Slots.Range.Label}**\n";
        text += dashboard.HasData
            ? $"✅ **OEE: {Percent(dashboard.Oee.Value)}** (A: {Percent(dashboard.Availability.Value)} · P: {Percent(dashboard.Performance.Value)} · Q: {Percent(dashboard.Quality.Value)})\n📦 Sản lượng đạt: **{Number(dashboard.Production.Good)}** / {Number(dashboard.Production.Total)}\n"
            : "Chưa có snapshot hoặc sản lượng trong kỳ đã chọn.\n";
        text += $"⚙️ Thiết bị hiện tại: **{running}/{live.Machines.Count} đang chạy**, {stopped} dừng.";

        if (unknown > 0)
            text += $" {unknown} máy chưa có dữ liệu xác nhận mới.";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(dashboard.TimeZoneId);
        text += $"\n🔔 **{open.TotalCount} cảnh báo đang mở** ({critical.TotalCount} nghiêm trọng).\nKỳ: {heading} (UTC{TimeZoneInfo.ConvertTime(context.Now, zone):zzz}).";
        text += $"\nTrạng thái máy: {TimeZoneInfo.ConvertTime(live.UpdatedAt, zone):dd/MM HH:mm:ss zzz}.";

        return new(
            text,
            [new("Mở Bảng điều khiển", "/")],
            ["Máy nào hiệu suất thấp hôm nay?", "Top 5 lý do dừng máy hôm nay", "Đạt được bao nhiêu % kế hoạch hôm nay?"],
            intent.Kind);
    }
}
