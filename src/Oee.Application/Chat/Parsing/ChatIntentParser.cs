using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Oee.Application.Chat;

public static partial class ChatIntentParser
{
    [GeneratedRegex("top\\s*(\\d{1,2})|(\\d{1,2})\\s*(?:ly do|nguyen nhan|may|thiet bi)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex TopNRegex();

    [GeneratedRegex("\\b(?:may|thiet bi|wu|line|day chuyen)\\s+(?=[a-z0-9\\-]*\\d)([a-z0-9\\-]{2,16})\\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex WorkUnitCodeRegex();

    [GeneratedRegex("so sanh\\s+(?:(?:may|thiet bi|wu|line|day chuyen)\\s+)?(?=[a-z0-9\\-]*\\d)([a-z0-9\\-]{2,16})\\s+(?:va|voi|vs)\\s+(?:(?:may|thiet bi|wu|line|day chuyen)\\s+)?(?=[a-z0-9\\-]*\\d)([a-z0-9\\-]{2,16})", RegexOptions.CultureInvariant, 100)]
    private static partial Regex CompareRegex();

    public static ChatIntent Parse(string question, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new ChatIntent(ChatIntentKind.Unknown, EmptySlots(today));
        }
        string normalized = Normalize(question);
        ChatSlots slots = ExtractSlots(normalized, today);

        return new ChatIntent(MatchIntent(normalized), slots);
    }

    private static ChatIntentKind MatchIntent(string n)
    {
        if ((Contains(n, "lam sao") || Contains(n, "lam the nao") || Contains(n, "the nao de") || Contains(n, "cach")
            || Contains(n, "huong dan")
            || Contains(n, "phai lam"))
            && DetectTopic(n) != null)
        {
            return ChatIntentKind.HowTo;
        }

        if (Contains(n, "tro giup") || Contains(n, "huong dan") || n == "help" || n == "menu"
            || Contains(n, "lam duoc gi")
            || Contains(n, "ban biet gi")
            || Contains(n, "hoi gi"))
        {
            return ChatIntentKind.Help;
        }

        if ((Contains(n, "tom tat") || Contains(n, "tong quan chung") || Contains(n, "tinh hinh chung")
            || Contains(n, "tinh hinh hom")
            || Contains(n, "snapshot")
            || Contains(n, "briefing"))
            && !WorkUnitCodeRegex().IsMatch(n))
        {
            return ChatIntentKind.DashboardSummary;
        }

        if ((Contains(n, "muc tieu") || Contains(n, "nguong") || Contains(n, "target") || Contains(n, "slo"))
            && (Contains(n, "liet ke") || Contains(n, "danh sach") || Contains(n, "tat ca") || Contains(n, "het han")
            || Contains(n, "sap het")
            || Contains(n, "muc tieu nao")
            || Contains(n, "target nao")
            || Contains(n, "nguong nao")))
        {
            return ChatIntentKind.OeeTargetsList;
        }

        if (Contains(n, "muc tieu") || Contains(n, "nguong") || Contains(n, "target") || Contains(n, "slo")
            || Contains(n, "kpi dat ra"))
        {
            return ChatIntentKind.OeeThresholds;
        }

        if ((Contains(n, "may nao") || Contains(n, "thiet bi nao"))
            && (Contains(n, "hong") || Contains(n, "su co") || Contains(n, "fault") || Contains(n, "breakdown")))
        {
            return ChatIntentKind.MostUnreliableMachines;
        }

        if (Contains(n, "mtbf") || Contains(n, "mttr") || Contains(n, "do tin cay") || Contains(n, "reliability"))
        {
            return ChatIntentKind.MostUnreliableMachines;
        }

        if ((Contains(n, "sku nao") || Contains(n, "san pham nao") || Contains(n, "ma hang nao")
            || (Contains(n, "sku") && Contains(n, "loi")))
            && (Contains(n, "loi") || Contains(n, "defect") || Contains(n, "ty le")))
        {
            return ChatIntentKind.DefectiveProducts;
        }

        if ((Contains(n, "qua lau") || Contains(n, "lau khong") || Contains(n, "dung lau")
            || Contains(n, "bao lau roi"))
            && (Contains(n, "may") || Contains(n, "thiet bi") || Contains(n, "dung")))
        {
            return ChatIntentKind.LongRunningDowntimes;
        }

        if (Contains(n, "ma loi") || Contains(n, "fault code") || Contains(n, "faultcode")
            || Contains(n, "ly do tu plc"))
        {
            return ChatIntentKind.PlcFaultCodeMappings;
        }

        if (Contains(n, "doanh nghiep") || Contains(n, "enterprise") || Contains(n, "nha may")
            || Contains(n, "factory")
            || Contains(n, "plant")
            || Contains(n, "co cau to chuc")
            || Contains(n, "cau truc to chuc")
            || Contains(n, "phan cap to chuc")
            || Contains(n, "to chuc cong ty")
            || Contains(n, "isa-95")
            || Contains(n, "isa 95"))
        {
            return ChatIntentKind.OrgStructure;
        }

        if (Contains(n, "anh xa") || Contains(n, "mapping") || Contains(n, "ma plc")
            || Contains(n, "external device")
            || Contains(n, "node id")
            || Contains(n, "nodeid"))
        {
            return ChatIntentKind.DeviceMappings;
        }

        if (Contains(n, "plc") || Contains(n, "opc ua") || Contains(n, "opcua")
            || (Contains(n, "ket noi") && (Contains(n, "may") || Contains(n, "thiet bi") || Contains(n, "session")))
            || Contains(n, "mat ket noi")
            || Contains(n, "disconnect")
            || Contains(n, "bad tag"))
        {
            return ChatIntentKind.PlcHealthStatus;
        }

        if (Contains(n, "ca nay con") || Contains(n, "het ca") || Contains(n, "ca sap het")
            || Contains(n, "ca con bao")
            || Contains(n, "khi nao het ca")
            || Contains(n, "ca hien tai con"))
        {
            return ChatIntentKind.CurrentShiftRemaining;
        }

        if ((Contains(n, "plan nao") || Contains(n, "ke hoach nao") || Contains(n, "active plan"))
            && (Contains(n, "dang chay") || Contains(n, "active") || Contains(n, "hien tai")))
        {
            return ChatIntentKind.ActivePlans;
        }

        if (Contains(n, "plan ke tiep") || Contains(n, "ke hoach ke tiep") || Contains(n, "plan sap toi")
            || Contains(n, "ke hoach sap toi")
            || Contains(n, "plan sau")
            || Contains(n, "ke hoach sau")
            || Contains(n, "next plan")
            || Contains(n, "upcoming plan"))
        {
            return ChatIntentKind.NextPlan;
        }

        if (Contains(n, "audit") || Contains(n, "nhat ky thao tac") || Contains(n, "ai sua")
            || Contains(n, "ai da sua")
            || Contains(n, "ai thay doi")
            || Contains(n, "lich su thao tac"))
        {
            return ChatIntentKind.AuditLogOverview;
        }

        if (CompareRegex().IsMatch(n))
        {
            return ChatIntentKind.CompareWorkUnits;
        }

        if ((Contains(n, "so voi") || Contains(n, "so ky truoc") || Contains(n, "tang hay giam")
            || Contains(n, "thay doi so"))
            && (Contains(n, "tuan") || Contains(n, "thang") || Contains(n, "ky") || Contains(n, "hom")))
        {
            return ChatIntentKind.PeriodComparison;
        }

        if ((Contains(n, "canh bao") || Contains(n, "alert"))
            && (Contains(n, "bao nhieu") || Contains(n, "thong ke") || Contains(n, "phan tich")
            || Contains(n, "tan suat")
            || Contains(n, "may nao nhieu")
            || Contains(n, "metric nao")
            || Contains(n, "chi so nao")))
        {
            return ChatIntentKind.AlertStatistics;
        }

        if (Contains(n, "canh bao") || Contains(n, "alert") || Contains(n, "warning") || Contains(n, "vi pham"))
        {
            return ChatIntentKind.OpenAlerts;
        }

        if (Contains(n, "ke hoach") || Contains(n, "plan") || Contains(n, "attainment")
            || Contains(n, "dat duoc bao nhieu")
            || Contains(n, "tien do"))
        {
            return ChatIntentKind.PlanAttainment;
        }

        if (WorkUnitCodeRegex().IsMatch(n) || BareUnitRegex().IsMatch(n))
        {
            if (Contains(n, "ly do") || Contains(n, "nguyen nhan") || Contains(n, "dung vi") || Contains(n, "dung do"))
            {
                return ChatIntentKind.WorkUnitLossReasons;
            }

            if (!Contains(n, "may nao") && !Contains(n, "thiet bi nao")
                && (Contains(n, "hien tai") || Contains(n, "the nao") || Contains(n, "dang chay") || Contains(n, "oee")
                || Contains(n, "hieu suat")
                || Contains(n, "san luong")
                || Contains(n, "tinh hinh")
                || Contains(n, "trang thai")))
            {
                return ChatIntentKind.WorkUnitDetail;
            }
        }

        if (Contains(n, "dang chay") || Contains(n, "bao nhieu may")
            || (Contains(n, "may") && Contains(n, "real time"))
            || (Contains(n, "may") && Contains(n, "live")))
        {
            return ChatIntentKind.LiveFleetStatus;
        }

        if ((Contains(n, "ly do dung may") || Contains(n, "ly do ton that") || Contains(n, "downtime reason")
            || (Contains(n, "ly do") && Contains(n, "khai bao")))
            && (Contains(n, "khai bao") || Contains(n, "danh sach") || Contains(n, "liet ke") || Contains(n, "bao nhieu")
            || Contains(n, "co nhung gi")
            || Contains(n, "loai nao")
            || Contains(n, "tat ca")))
        {
            return ChatIntentKind.DowntimeReasonsList;
        }

        if (DetectMasterDataEntity(n) != null
            && (Contains(n, "liet ke") || Contains(n, "danh sach") || Contains(n, "bao nhieu") || Contains(n, "tat ca")
            || Contains(n, "khai bao")
            || Contains(n, "co nhung")))
        {
            return ChatIntentKind.MasterDataList;
        }

        if ((Contains(n, "ly do") || Contains(n, "nguyen nhan") || Contains(n, "pareto") || Contains(n, "ton that"))
            && (Contains(n, "dung") || Contains(n, "ton that") || Contains(n, "loi")))
        {
            return ChatIntentKind.TopLossReasons;
        }

        if ((Contains(n, "may nao") || Contains(n, "thiet bi nao"))
            && (Contains(n, "dung nhieu") || Contains(n, "dung lau") || Contains(n, "dung nhat")))
        {
            return ChatIntentKind.TopDowntimeMachines;
        }

        if ((Contains(n, "may nao") || Contains(n, "thiet bi nao"))
            && (Contains(n, "thap") || Contains(n, "te nhat") || Contains(n, "kem") || Contains(n, "yeu")))
        {
            return ChatIntentKind.LowPerformanceMachines;
        }

        if ((Contains(n, "ca nao") || Contains(n, "ngay nao"))
            && (Contains(n, "thap") || Contains(n, "te") || Contains(n, "kem") || Contains(n, "yeu")))
        {
            return ChatIntentKind.WorstShift;
        }

        if ((Contains(n, "ca nao") || Contains(n, "ngay nao"))
            && (Contains(n, "cao nhat") || Contains(n, "tot nhat") || Contains(n, "dat nhat")
            || (Contains(n, "tot") && !Contains(n, "khong tot"))))
        {
            return ChatIntentKind.BestShift;
        }

        if ((Contains(n, "may nao") || Contains(n, "thiet bi nao"))
            && (Contains(n, "tot nhat") || Contains(n, "cao nhat") || Contains(n, "nhanh nhat")
            || Contains(n, "hieu suat cao")
            || Contains(n, "best")
            || Contains(n, "top performer")))
        {
            return ChatIntentKind.TopPerformers;
        }

        if (Contains(n, "san luong") || Contains(n, "san pham loi") || Contains(n, "defect") || Contains(n, "good")
            || Contains(n, "san pham dat"))
        {
            return ChatIntentKind.ProductionTotal;
        }

        if (Contains(n, "xu huong oee") || Contains(n, "oee tung ngay") || Contains(n, "oee theo ngay")
            || Contains(n, "oee theo tuan")
            || Contains(n, "timeline oee")
            || Contains(n, "oee timeline")
            || (Contains(n, "oee") && (Contains(n, "tung ngay") || Contains(n, "moi ngay") || Contains(n, "bien thien"))))
        {
            return ChatIntentKind.OeeTimeline;
        }

        if (Contains(n, "oee") || Contains(n, "hieu suat") || Contains(n, "san sang") || Contains(n, "chat luong"))
        {
            return ChatIntentKind.OeeSummary;
        }

        return ChatIntentKind.Unknown;
    }

    private static ChatSlots ExtractSlots(string n, DateOnly today)
    {
        ChatTimeRange range = ExtractTimeRange(n, today);
        int topN = ExtractTopN(n);
        string? topic = Contains(n, "chua anh xa")
            ? "unmapped"
            : Contains(n, "sap het han") || Contains(n, "sap het")
            ? "expiring-target"
            : Contains(n, "tuan nay") && Contains(n, "tuan truoc")
            ? "compare-week"
            : Contains(n, "thang nay") && Contains(n, "thang truoc")
            ? "compare-month"
            : Contains(n, "ngay nao") ? "day-ranking" : DetectTopic(n);
        string? entity = DetectMasterDataEntity(n);
        Match cmp = CompareRegex().Match(n);

        if (cmp.Success)
        {
            return new ChatSlots(
                range,
                topN,
                cmp.Groups[1].Value.ToUpperInvariant(),
                cmp.Groups[2].Value.ToUpperInvariant(),
                topic,
                entity);
        }
        Match single = WorkUnitCodeRegex().Match(n);

        if (!single.Success)
            single = BareUnitRegex().Match(n);
        string? codeA = (single.Success ? single.Groups[1].Value.ToUpperInvariant() : null);

        return new ChatSlots(range, topN, codeA, null, topic, entity);
    }

    private static string? DetectMasterDataEntity(string n)
    {
        if (Contains(n, "doanh nghiep") || Contains(n, "enterprise"))
            return "enterprise";

        if (Contains(n, "nha may") || Contains(n, "factory") || Contains(n, "plant"))
            return "site";

        if (Contains(n, "khu vuc"))
            return "area";

        if (Contains(n, "dinh muc toc do") || Contains(n, "toc do chuan") || Contains(n, "standard speed")
            || Contains(n, "speed standard")
            || Contains(n, "workunitstandard"))
        {
            return "speed-standard";
        }

        if (Contains(n, "nhom thiet bi") || Contains(n, "nhom may") || Contains(n, "workunitgroup")
            || Contains(n, "work unit group"))
        {
            return "workunit-group";
        }

        if (Contains(n, "cong doan") || Contains(n, "process"))
        {
            return "process";
        }

        if (Contains(n, "nguoi dung") || Contains(n, "tai khoan") || Contains(n, "user") || Contains(n, "account"))
        {
            return "user";
        }

        if (Contains(n, "san pham") || Contains(n, "sku") || Contains(n, "ma hang") || Contains(n, "product"))
        {
            return "product";
        }

        if (Contains(n, "ca lam viec") || Contains(n, "ca san xuat") || Contains(n, "shift"))
        {
            return "shift";
        }

        if (Contains(n, "thiet bi") || Contains(n, "day chuyen") || Contains(n, "work unit"))
        {
            return "workunit";
        }

        return null;
    }

    private static string? DetectTopic(string n)
    {
        if (Contains(n, "ma loi") || Contains(n, "fault code") || Contains(n, "faultcode"))
        {
            return "fault-code";
        }

        if (Contains(n, "anh xa") || Contains(n, "mapping") || Contains(n, "ket noi plc")
            || Contains(n, "ket noi opc"))
        {
            return "mapping";
        }

        if (Contains(n, "dinh muc toc do") || Contains(n, "dinh muc") || Contains(n, "toc do chuan")
            || Contains(n, "standard speed")
            || Contains(n, "speed standard")
            || Contains(n, "workunitstandard"))
        {
            return "speed-standard";
        }

        if (Contains(n, "ke hoach san xuat") || Contains(n, "ke hoach") || Contains(n, "tao plan")
            || Contains(n, "sua plan")
            || Contains(n, "plan san xuat")
            || Contains(n, "production plan"))
        {
            return "plan";
        }

        if (Contains(n, "phan loai ly do") || Contains(n, "phan loai su kien") || Contains(n, "cap nhat ly do")
            || Contains(n, "sua ly do")
            || Contains(n, "doi ly do")
            || Contains(n, "reclassify")
            || Contains(n, "su kien dung may")
            || Contains(n, "downtime event"))
        {
            return "downtime-reason-update";
        }

        if (Contains(n, "ly do dung may") || Contains(n, "ly do ton that") || Contains(n, "downtime reason")
            || Contains(n, "danh muc ly do")
            || Contains(n, "loss category"))
        {
            return "downtime-reason";
        }

        if (Contains(n, "doanh nghiep") || Contains(n, "enterprise") || Contains(n, "nha may")
            || Contains(n, "factory")
            || Contains(n, "plant")
            || Contains(n, "co cau to chuc")
            || Contains(n, "cau truc to chuc")
            || Contains(n, "phan cap to chuc")
            || Contains(n, "to chuc cong ty"))
        {
            return "org-structure";
        }
        string? text = DetectMasterDataEntity(n);

        if (text != null && !(text == "speed-standard"))
        {
            return "master-data";
        }

        if (Contains(n, "xac nhan canh bao") || Contains(n, "ack alert") || Contains(n, "xu ly canh bao")
            || Contains(n, "giai quyet canh bao")
            || Contains(n, "resolve alert")
            || Contains(n, "ack canh bao")
            || (Contains(n, "canh bao") && (Contains(n, "xac nhan") || Contains(n, "giai quyet") || Contains(n, "xu ly"))))
        {
            return "alert-handling";
        }

        if (Contains(n, "slo") || Contains(n, "oee target") || Contains(n, "slo target") || Contains(n, "muc tieu")
            || (Contains(n, "nguong")
            && (Contains(n, "oee") || Contains(n, "kpi") || Contains(n, "a/p/q") || Contains(n, "san sang")
            || Contains(n, "hieu suat")
            || Contains(n, "chat luong"))))
        {
            return "slo-target";
        }

        return null;
    }

    private static ChatTimeRange ExtractTimeRange(string n, DateOnly today)
    {
        var explicitDates = DateRegex().Matches(n)
            .Select(match => DateOnly.TryParseExact(match.Value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? (DateOnly?)date
                : null)
            .ToArray();

        if (explicitDates.Length > 0)
        {
            if (explicitDates.Any(date => date is null))
                throw new ArgumentException("Ngày không hợp lệ. Hãy dùng dd/MM/yyyy.");
            var from = explicitDates[0]!.Value;
            var to = explicitDates.Length > 1 ? explicitDates[1]!.Value : from;

            if (to < from || to.DayNumber - from.DayNumber >= 366)
                throw new ArgumentException("Khoảng ngày phải theo thứ tự và không quá 366 ngày.");

            return new(from, to, $"{from:dd/MM/yyyy}–{to:dd/MM/yyyy}");
        }

        if (Contains(n, "hom qua"))
            return new(today.AddDays(-1), today.AddDays(-1), "hôm qua");

        if (Contains(n, "hom nay"))
            return new(today, today, "hôm nay");
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var month = new DateOnly(today.Year, today.Month, 1);

        if (Contains(n, "tuan nay"))
            return new(monday, today, "tuần này");

        if (Contains(n, "thang nay"))
            return new(month, today, "tháng này");

        if (Contains(n, "7 ngay") || Contains(n, "7d"))
            return new(today.AddDays(-6), today, "7 ngày qua");

        if (Contains(n, "30 ngay") || Contains(n, "30d"))
            return new(today.AddDays(-29), today, "30 ngày qua");

        if (Contains(n, "tuan truoc"))
            return new(monday.AddDays(-7), monday.AddDays(-1), "tuần trước");

        if (Contains(n, "thang truoc"))
            return new(month.AddMonths(-1), month.AddDays(-1), "tháng trước");

        return new(today, today, "hôm nay");
    }

    [GeneratedRegex(@"\b\d{1,2}/\d{1,2}/\d{4}\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\b((?:m|line)-?\d{1,8})\b", RegexOptions.CultureInvariant, 100)]
    private static partial Regex BareUnitRegex();

    private static int ExtractTopN(string n)
    {
        Match m = TopNRegex().Match(n);

        if (!m.Success)
        {
            return 5;
        }

        if (!int.TryParse(
            m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var v))
        {
            return 5;
        }

        return Math.Clamp(v, 1, 20);
    }

    internal static string Normalize(string input)
    {
        string text = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(text.Length);
        string text2 = text;

        foreach (char c in text2)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                if (c == 'đ')
                {
                    sb.Append('d');
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        return SpacesRegex().Replace(sb.ToString(), " ").Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex SpacesRegex();

    private static bool Contains(string haystack, string needle)
    {
        var start = 0;
        while ((start = haystack.IndexOf(needle, start, StringComparison.Ordinal)) >= 0)
        {
            var end = start + needle.Length;

            if ((start == 0 || !char.IsLetterOrDigit(haystack[start - 1]))
                && (end == haystack.Length || !char.IsLetterOrDigit(haystack[end])))
                return true;
            start = end;
        }

        return false;
    }

    private static ChatSlots EmptySlots(DateOnly today)
    {
        DateOnly dateOnly = today;

        return new ChatSlots(new ChatTimeRange(dateOnly, dateOnly, "hôm nay"), 5, null, null);
    }
}
