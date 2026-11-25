using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Modules;
using Oee.Application.Oee;
using Oee.Application.Reporting;
using Oee.Domain.Organization;

namespace Oee.Application.Chat;

internal sealed class ChatContext(
    IReportAnalysisQueryService reports,
    IDashboardQueryService dashboard,
    ILiveOeeQueryService live,
    IProductionModuleQueryService production,
    IAlertReadStore alerts,
    IChatReadStore reads,
    IClock clock)
    : IUseCaseContext
{
    internal IReportAnalysisQueryService Reports => reports;
    internal IDashboardQueryService Dashboard => dashboard;
    internal ILiveOeeQueryService Live => live;
    internal IProductionModuleQueryService Production => production;
    internal IAlertReadStore Alerts => alerts;
    internal IChatReadStore Reads => reads;
    internal DateTimeOffset Now => clock.UtcNow;

    internal static ReportFilterOption? ResolveUnit(string? code, ReportFilterOptions filters)
    {
        if (code is null)
            return null;
        var exact = filters.WorkUnits.Where(unit => string.Equals(unit.Code, code, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (exact.Length == 1)
            return exact[0];
        // M01 và M-001 chỉ được coi là cùng mã khi kết quả duy nhất.
        static string Key(string value)
        {
            var text = string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
            var index = 0;
            while (index < text.Length && char.IsLetter(text[index]))
                index++;

            return text[..index] + text[index..].TrimStart('0');
        }
        var matches = filters.WorkUnits.Where(unit => Key(unit.Code) == Key(code)).ToArray();

        if (matches.Length != 1)
            throw new ArgumentException($"Không tìm thấy duy nhất thiết bị {code}. Hãy nhập đúng mã trong danh sách thiết bị.");

        return matches[0];
    }

    internal static (DateTimeOffset From, DateTimeOffset Until) Bounds(
        ChatTimeRange range,
        string zoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var period = ShiftPeriodResolver.Resolve(range.From, TimeOnly.MinValue, TimeOnly.MinValue, zone);
        var end = ShiftPeriodResolver.Resolve(range.To, TimeOnly.MinValue, TimeOnly.MinValue, zone);

        return (period.Start.ToUniversalTime(), end.End.ToUniversalTime());
    }
}
