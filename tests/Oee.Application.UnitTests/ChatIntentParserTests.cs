using Oee.Application.Chat;

namespace Oee.Application.UnitTests;

public sealed class ChatIntentParserTests
{
    private static readonly DateOnly Today = new(2026, 10, 26);

    [Theory]
    [InlineData("OEE hôm nay", ChatIntentKind.OeeSummary)]
    [InlineData("Tóm tắt tình hình hôm nay", ChatIntentKind.DashboardSummary)]
    [InlineData("Tổng sản lượng hôm nay", ChatIntentKind.ProductionTotal)]
    [InlineData("Máy nào hiệu suất thấp hôm nay?", ChatIntentKind.LowPerformanceMachines)]
    [InlineData("Máy nào tốt nhất tuần này?", ChatIntentKind.TopPerformers)]
    [InlineData("Top 5 lý do dừng máy hôm nay", ChatIntentKind.TopLossReasons)]
    [InlineData("Máy nào dừng nhiều nhất tuần này?", ChatIntentKind.TopDowntimeMachines)]
    [InlineData("Ca nào OEE thấp nhất tuần này?", ChatIntentKind.WorstShift)]
    [InlineData("Ca nào OEE cao nhất tuần này?", ChatIntentKind.BestShift)]
    [InlineData("So sanh may M01 voi may M03", ChatIntentKind.CompareWorkUnits)]
    [InlineData("OEE tuần này so với tuần trước?", ChatIntentKind.PeriodComparison)]
    [InlineData("Bao nhiêu máy đang chạy?", ChatIntentKind.LiveFleetStatus)]
    [InlineData("Có máy nào đang dừng quá lâu không?", ChatIntentKind.LongRunningDowntimes)]
    [InlineData("Ca hiện tại còn bao lâu nữa?", ChatIntentKind.CurrentShiftRemaining)]
    [InlineData("Máy M01 hiện tại thế nào?", ChatIntentKind.WorkUnitDetail)]
    [InlineData("Máy M01 thường dừng vì lý do gì?", ChatIntentKind.WorkUnitLossReasons)]
    [InlineData("Plan nào đang chạy?", ChatIntentKind.ActivePlans)]
    [InlineData("Plan kế tiếp của máy M01?", ChatIntentKind.NextPlan)]
    [InlineData("Đạt được bao nhiêu % kế hoạch hôm nay?", ChatIntentKind.PlanAttainment)]
    [InlineData("Có cảnh báo nào đang mở?", ChatIntentKind.OpenAlerts)]
    [InlineData("Thống kê cảnh báo tuần này", ChatIntentKind.AlertStatistics)]
    [InlineData("Ngưỡng OEE đang đặt bao nhiêu?", ChatIntentKind.OeeThresholds)]
    [InlineData("Danh sách ngưỡng SLO đang đặt", ChatIntentKind.OeeTargetsList)]
    [InlineData("Trạng thái PLC thế nào?", ChatIntentKind.PlcHealthStatus)]
    [InlineData("Máy nào chưa ánh xạ PLC?", ChatIntentKind.DeviceMappings)]
    [InlineData("Liệt kê ánh xạ mã lỗi PLC", ChatIntentKind.PlcFaultCodeMappings)]
    [InlineData("Liệt kê lý do dừng máy", ChatIntentKind.DowntimeReasonsList)]
    [InlineData("Liệt kê sản phẩm", ChatIntentKind.MasterDataList)]
    [InlineData("Cấu trúc tổ chức", ChatIntentKind.OrgStructure)]
    [InlineData("Hôm nay ai đã sửa gì?", ChatIntentKind.AuditLogOverview)]
    [InlineData("Liệt kê bản ghi sản lượng hôm nay", ChatIntentKind.ProductionTotal)]
    [InlineData("SKU nào có tỷ lệ lỗi cao nhất tuần này?", ChatIntentKind.DefectiveProducts)]
    [InlineData("Máy nào hỏng nhiều nhất tuần này?", ChatIntentKind.MostUnreliableMachines)]
    [InlineData("Xu hướng OEE 7 ngày qua", ChatIntentKind.OeeTimeline)]
    [InlineData("Cách tạo kế hoạch sản xuất?", ChatIntentKind.HowTo)]
    [InlineData("Trợ giúp", ChatIntentKind.Help)]
    [InlineData("thời tiết", ChatIntentKind.Unknown)]
    public void RecognizesRecoveredQuestionFamilies(string question, ChatIntentKind expected) =>
        Assert.Equal(expected, ChatIntentParser.Parse(question, Today).Kind);

    [Fact]
    public void CalendarWeekAndMonthDifferFromRollingWindows()
    {
        Assert.Equal(new DateOnly(2026, 10, 26), ChatIntentParser.Parse("OEE tuần này", Today).Slots.Range.From);
        Assert.Equal(new DateOnly(2026, 10, 20), ChatIntentParser.Parse("OEE 7 ngày qua", Today).Slots.Range.From);
        Assert.Equal(new DateOnly(2026, 10, 1), ChatIntentParser.Parse("OEE tháng này", Today).Slots.Range.From);
        Assert.Equal(new DateOnly(2026, 9, 27), ChatIntentParser.Parse("OEE 30 ngày qua", Today).Slots.Range.From);
        Assert.Equal(new DateOnly(2026, 9, 30), ChatIntentParser.Parse("OEE tháng trước", Today).Slots.Range.To);
    }

    [Fact]
    public void ExtractsMachinePairAndBoundedTopCountAndExplicitDates()
    {
        var intent = ChatIntentParser.Parse("So sánh máy M-001 với máy M03 từ 01/10/2026 đến 15/10/2026 top 99", Today);
        Assert.Equal("M-001", intent.Slots.WorkUnitCodeA);
        Assert.Equal("M03", intent.Slots.WorkUnitCodeB);
        Assert.Equal(20, intent.Slots.TopN);
        Assert.Equal(new DateOnly(2026, 10, 1), intent.Slots.Range.From);
        Assert.Equal(new DateOnly(2026, 10, 15), intent.Slots.Range.To);
    }

    [Theory]
    [InlineData("M01 hiện tại thế nào?", "M01", ChatIntentKind.WorkUnitDetail, null)]
    [InlineData("Ngày nào OEE thấp nhất tháng này?", null, ChatIntentKind.WorstShift, "day-ranking")]
    [InlineData("Ngưỡng nào sắp hết hạn?", null, ChatIntentKind.OeeTargetsList, "expiring-target")]
    [InlineData("OEE tuần này so với tuần trước?", null, ChatIntentKind.PeriodComparison, "compare-week")]
    [InlineData("OEE tháng này so với tháng trước?", null, ChatIntentKind.PeriodComparison, "compare-month")]
    public void ExtractsSpecificQuestionMeaning(string question, string? code, ChatIntentKind kind, string? topic)
    {
        var intent = ChatIntentParser.Parse(question, Today);
        Assert.Equal(kind, intent.Kind);
        Assert.Equal(code, intent.Slots.WorkUnitCodeA);
        Assert.Equal(topic, intent.Slots.Topic);
    }

    [Theory]
    [InlineData("OEE 31/02/2026")]
    [InlineData("OEE từ 20/10/2026 đến 01/10/2026")]
    [InlineData("OEE từ 01/01/2020 đến 01/01/2026")]
    public void RejectsInvalidDatesInsteadOfSilentlyQueryingToday(string question) =>
        Assert.Throws<ArgumentException>(() => ChatIntentParser.Parse(question, Today));
}
