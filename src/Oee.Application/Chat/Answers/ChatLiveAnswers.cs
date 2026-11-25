using Oee.Application.Contracts.Reporting;
using Oee.Domain.Organization;
using static Oee.Application.Chat.ChatFormatting;

namespace Oee.Application.Chat;

internal sealed class ChatLiveAnswers(ChatContext context)
{
    internal async Task<ChatAnswerDto> AnswerAsync(
        ChatIntent intent,
        ReportFilterOption? unit,
        string zoneId,
        ReportFilterOptions filters,
        CancellationToken cancellationToken)
    {
        if (intent.Kind == ChatIntentKind.CurrentShiftRemaining)
        {
            var lookup = await context.Production.LookupsAsync(cancellationToken);
            var selectedSite = unit is null ? null : lookup.WorkUnits.FirstOrDefault(x => x.Id == unit.Id)?.SiteId;

            if (unit is not null && selectedSite is null)
                return Answer(
                    intent.Kind,
                    "Chưa xác định được nhà máy của thiết bị để đọc ca.",
                    "Ca làm việc",
                    "/master/shifts");
            var periods = new List<string>();

            foreach (var shift in lookup.Shifts.Where(x => selectedSite is null || x.SiteId == selectedSite))
            {
                if (shift.StartTime is not { } start || shift.EndTime is not { } finish)
                    continue;
                var site = filters.Sites.FirstOrDefault(x => x.Id == shift.SiteId);
                var siteZone = TimeZoneInfo.FindSystemTimeZoneById(site?.Detail ?? zoneId);
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.Now, siteZone).DateTime);

                foreach (var date in new[] { today.AddDays(-1), today })
                {
                    var period = ShiftPeriodResolver.Resolve(date, start, finish, siteZone);

                    if (context.Now < period.Start || context.Now >= period.End)
                        continue;
                    periods.Add($"{site?.Code ?? "Nhà máy"} / {shift.Code} · {shift.Name}: còn {(period.End - context.Now).TotalMinutes:N0} phút; kết thúc {period.End:dd/MM HH:mm zzz}.");
                }
            }

            return Answer(
                intent.Kind,
                periods.Count == 0
                    ? "Không có ca làm việc bao phủ thời điểm hiện tại."
                    : string.Join('\n', periods.Take(intent.Slots.TopN)),
                "Ca làm việc",
                "/master/shifts");
        }
        var model = await context.Live.GetAsync(new(), cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var machines = model.Machines.Where(x => unit is null || x.WorkUnitId == unit.Id).ToArray();
        string text;

        if (intent.Kind == ChatIntentKind.LongRunningDowntimes)
        {
            var stops = machines.Where(x => x.OperationalState == "Stopped" && x.ConnectivityState == "Connected"
                    && !x.IsStale
                    && x.DowntimeStartedAt.HasValue
                    && x.DowntimeStartedAt <= context.Now)
                .OrderBy(x => x.DowntimeStartedAt)
                .Take(intent.Slots.TopN)
                .ToArray();
            text = stops.Length == 0
                ? "Không có máy được xác nhận đang dừng kèm thời điểm bắt đầu."
                : "Máy đang dừng (xếp theo thời gian dừng dài nhất):\n" + string.Join(
                '\n',
                stops.Select(x => $"{x.Code}: {(context.Now - x.DowntimeStartedAt!.Value).TotalMinutes:N1} phút; {x.DowntimeReason ?? "chưa phân loại"}; kết nối {x.ConnectivityState}."));
        }
        else
        {
            var confirmed = machines.Where(x => x.ConnectivityState == "Connected" && !x.IsStale).ToArray();
            text = $"Trạng thái hiện tại: {machines.Length} máy; {confirmed.Count(x => x.OperationalState == "Running")} đang chạy; {confirmed.Count(x => x.OperationalState == "Stopped")} đang dừng; {machines.Length - confirmed.Length} máy chưa có dữ liệu xác nhận mới.\n";
            text += string.Join(
                '\n',
                machines.Take(intent.Slots.TopN)
                    .Select(x =>
                    $"{x.Code} · {x.Name}: {x.ConnectivityState}{(x.IsStale ? " (dữ liệu cũ)" : "")}{(x.IsCounterStalled ? " (bộ đếm chưa thay đổi)" : "")}; vận hành {(x.ConnectivityState == "Connected" && !x.IsStale ? x.OperationalState : "chưa xác nhận")}; OEE ca {Percent(x.Oee)}; đạt {Number(x.ProductionPeriods?.ShiftQuantity)}; kế hoạch {x.CurrentPlan ?? "chưa có"}."));
        }
        text += $"\nSnapshot: {TimeZoneInfo.ConvertTime(model.UpdatedAt, zone):dd/MM/yyyy HH:mm:ss zzz}.";

        if (model.StreamState != "Live")
            text += " Nguồn realtime đang kết nối lại.";

        return Answer(intent.Kind, text, "Mở Live OEE", "/live-oee");
    }
}
