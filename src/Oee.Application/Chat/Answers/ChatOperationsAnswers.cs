using Oee.Application.Contracts.Reporting;
using static Oee.Application.Chat.ChatFormatting;

namespace Oee.Application.Chat;

internal sealed class ChatOperationsAnswers(ChatContext context)
{
    internal async Task<ChatAnswerDto> AnswerAsync(
        ChatIntent intent,
        ReportFilterOption? unit,
        string zoneId,
        CancellationToken cancellationToken)
    {
        var slots = intent.Slots;

        if (intent.Kind == ChatIntentKind.MasterDataList && slots.MasterDataEntity == "process")
            return Answer(
                intent.Kind,
                "Project hiện tại không có danh mục công đoạn riêng. Cây tổ chức gồm Doanh nghiệp → Nhà máy → Khu vực → Nhóm máy → Thiết bị.",
                "Cơ cấu tổ chức",
                "/equipment/hierarchy");
        var bounds = ChatContext.Bounds(slots.Range, zoneId);
        var heading = $"{slots.Range.From:dd/MM/yyyy}–{slots.Range.To:dd/MM/yyyy}, {unit?.Code ?? "toàn hệ thống"}, {zoneId}";

        if (intent.Kind is ChatIntentKind.PlanAttainment or ChatIntentKind.ActivePlans or ChatIntentKind.NextPlan)
        {
            var from = intent.Kind == ChatIntentKind.PlanAttainment ? bounds.From : context.Now;
            var until = intent.Kind == ChatIntentKind.PlanAttainment
                ? bounds.Until
                : intent.Kind == ChatIntentKind.ActivePlans ? from.AddTicks(1) : from.AddDays(30);
            var plans = await context.Reads.PlansAsync(
                unit?.Id,
                from,
                until,
                intent.Kind == ChatIntentKind.NextPlan,
                slots.TopN,
                cancellationToken);
            var text = intent.Kind == ChatIntentKind.PlanAttainment
                ? $"Kế hoạch giao khoảng {heading}: {plans.TotalCount} kế hoạch.\nKế hoạch: {Number(plans.Planned)}; thực tế: {Number(plans.Actual)}; đạt {Percent(plans.Planned == 0 ? null : plans.Actual / plans.Planned)}. Sản lượng là tổng ghi nhận của các kế hoạch này."
                : intent.Kind == ChatIntentKind.ActivePlans
                ? $"Có {plans.TotalCount} kế hoạch đang trong thời gian chạy."
                : $"Có {plans.TotalCount} kế hoạch bắt đầu trong 30 ngày tới.";
            text += "\n" + string.Join(
                '\n',
                plans.Items.Select(x => $"{x.Code} · {x.WorkUnit} · {x.Product}: {Number(x.ActualQuantity)}/{Number(x.PlannedQuantity)} ({Percent(x.Attainment)}); {x.Start.ToOffset(TimeZoneInfo.FindSystemTimeZoneById(zoneId).GetUtcOffset(x.Start)):dd/MM HH:mm}–{x.End.ToOffset(TimeZoneInfo.FindSystemTimeZoneById(zoneId).GetUtcOffset(x.End)):dd/MM HH:mm}."));

            if (plans.TotalCount > plans.Items.Count)
                text += $"\nHiển thị {plans.Items.Count}/{plans.TotalCount} kế hoạch; tổng ở trên tính toàn bộ kết quả.";

            return Answer(intent.Kind, text, "Kế hoạch sản xuất", "/production/plans");
        }

        if (intent.Kind == ChatIntentKind.OpenAlerts)
        {
            // Open không áp dụng khoảng ngày mặc định để không giấu cảnh báo còn mở từ hôm trước.
            var page = await context.Alerts.ListAsync(new(Search: unit?.Code, State: "Open", PageSize: slots.TopN), cancellationToken);

            return Answer(
                intent.Kind,
                $"Có {page.TotalCount} cảnh báo đang mở; hiển thị {page.Items.Count}.\n" + string.Join(
                    '\n',
                    page.Items.Select(x => $"{x.WorkUnit} · {x.Metric}: {Percent(x.Actual)} / ngưỡng {Percent(x.Target)}; {x.Severity}; {x.TriggeredAt:dd/MM HH:mm zzz}.")),
                "Cảnh báo SLO",
                "/alerts");
        }

        if (intent.Kind is ChatIntentKind.OeeThresholds or ChatIntentKind.OeeTargetsList)
        {
            var expiring = slots.Topic == "expiring-target";
            var page = await context.Reads.TargetsAsync(
                unit?.Id,
                slots.Range.To,
                slots.TopN,
                cancellationToken,
                expiring ? slots.Range.To.AddDays(30) : null);
            var text = $"Có {page.TotalCount} cấu hình ngưỡng hiệu lực ngày {slots.Range.To:dd/MM/yyyy}; hiển thị {page.Items.Count}.\n";

            if (expiring)
                text += "Chỉ gồm cấu hình hết hạn trong 30 ngày tiếp theo.\n";
            text += string.Join(
                '\n',
                page.Items.Select(x => $"{x.Scope} · {x.PeriodType}/{x.PeriodCode}: OEE {Percent(x.Oee)}, A {Percent(x.Availability)}, P {Percent(x.Performance)}, Q {Percent(x.Quality)}; từ {x.From:dd/MM/yyyy} đến {(x.To.HasValue
? x.To.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
: "không giới hạn")}."));
            text += "\nĐây là danh sách cấu hình theo phạm vi và loại kỳ; không coi một ngưỡng là áp dụng cho mọi máy/kỳ.";

            return Answer(intent.Kind, text, "Mục tiêu SLO", "/alerts/targets");
        }

        var kind = intent.Kind switch
        {
            ChatIntentKind.DeviceMappings when slots.Topic == "unmapped" => ChatCatalogKind.UnmappedWorkUnit,
            ChatIntentKind.DeviceMappings => ChatCatalogKind.DeviceMapping,
            ChatIntentKind.PlcFaultCodeMappings => ChatCatalogKind.FaultMapping,
            ChatIntentKind.DowntimeReasonsList => ChatCatalogKind.DowntimeReason,
            ChatIntentKind.AuditLogOverview => ChatCatalogKind.Audit,
            ChatIntentKind.AlertStatistics => ChatCatalogKind.AlertStatistics,
            ChatIntentKind.OrgStructure => slots.MasterDataEntity == "enterprise"
                ? ChatCatalogKind.Enterprise
                : slots.MasterDataEntity == "site" ? ChatCatalogKind.Site : ChatCatalogKind.WorkUnit,
            _ => slots.MasterDataEntity switch
            {
                "speed-standard" => ChatCatalogKind.Standard,
                "workunit-group" => ChatCatalogKind.WorkCenter,
                "area" => ChatCatalogKind.Area,
                "user" => ChatCatalogKind.User,
                "product" => ChatCatalogKind.Product,
                "shift" => ChatCatalogKind.Shift,
                _ => ChatCatalogKind.WorkUnit
            }
        };
        var pageData = await context.Reads.CatalogAsync(
            kind,
            unit?.Id,
            bounds.From,
            bounds.Until,
            slots.Range.To,
            slots.TopN,
            cancellationToken);
        var (title, href) = kind switch
        {
            ChatCatalogKind.DeviceMapping or ChatCatalogKind.UnmappedWorkUnit => ("Ánh xạ thiết bị", "/plc/mappings"),
            ChatCatalogKind.FaultMapping => ("Ánh xạ mã lỗi", "/plc/faults"),
            ChatCatalogKind.DowntimeReason => ("Lý do dừng máy", "/master/downtime-reasons"),
            ChatCatalogKind.Standard => ("Định mức tốc độ", "/master/work-unit-standards"),
            ChatCatalogKind.Product => ("Sản phẩm", "/master/products"),
            ChatCatalogKind.Shift => ("Ca làm việc", "/master/shifts"),
            ChatCatalogKind.User => ("Người dùng", "/users"),
            ChatCatalogKind.Audit => ("Nhật ký thao tác", "/audit"),
            ChatCatalogKind.AlertStatistics => ("Thống kê cảnh báo", "/alerts"),
            _ => ("Cơ cấu tổ chức", "/equipment/hierarchy")
        };
        var result = $"{title}: {pageData.TotalCount} bản ghi/đối tượng; hiển thị {pageData.Items.Count}.";

        if (kind == ChatCatalogKind.AlertStatistics)
            result = $"Có {pageData.TotalEvents} cảnh báo trong {pageData.TotalCount} nhóm máy × chỉ số × trạng thái; hiển thị {pageData.Items.Count} nhóm.";

        if (kind is ChatCatalogKind.Audit or ChatCatalogKind.AlertStatistics)
            result += $"\nKỳ: {heading}.";
        result += "\n" + string.Join(
            '\n',
            pageData.Items.Select(x =>
                {
                    var quantity = x.Quantity.HasValue
                        ? kind == ChatCatalogKind.Standard
                        ? x.Quantity.Value.ToString("G29", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))
                        : Number(x.Quantity)
                        : null;

                    return $"{x.Code} · {x.Name}{(x.Detail is null ? "" : " · " + x.Detail)}{(quantity is null ? "" : ": " + quantity)}{(x.At.HasValue
            ? " · " + x.At.Value.ToString("dd/MM HH:mm zzz", System.Globalization.CultureInfo.InvariantCulture)
            : "")}";
                }));

        return Answer(intent.Kind, result, title, href);
    }
}
