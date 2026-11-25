using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Oee;
using Oee.Domain.Production;
using Oee.Tests.Fixtures;

namespace Oee.Domain.UnitTests;

/// <summary>
/// Các Unit Test bảo vệ quy tắc Domain liên quan đến kế hoạch, downtime, target OEE và vòng đời cảnh báo.
/// </summary>
/// <remarks>
/// Test gọi trực tiếp rule thuần trong Domain, vì vậy không đọc/ghi SQL và không phụ thuộc giao diện Blazor.
/// </remarks>
public sealed class ProductionDowntimeTargetRulesTests
{
    [Fact]
    public void PlanWithOutputRequiresCorrectionAndIncrementsVersion()
    {
        var plan = DomainTestData.Create<ProductionPlanEntity>(("Code", "P1"), ("VersionSeriesId", Guid.NewGuid()), ("Version", 1));
        // Act & Assert: không cho sửa trực tiếp; correction phải thay thế bản cũ và tăng version.
        Assert.Throws<InvalidOperationException>(() => ProductionRules.EnsureCanEdit(plan, true));
        var correction = plan.CreateCorrection(DateTimeOffset.UtcNow);
        Assert.Equal(2, correction.Version);
        Assert.Null(correction.DeletedAt);
        Assert.Equal("P1-V2", correction.Code);
        Assert.Equal(plan.Id, correction.ReplacesPlanId);
    }

    [Fact]
    public void PlansAreEffectiveByDefaultAndDeleteDoesNotNeedReason()
    {
        var plan = DomainTestData.Create<ProductionPlanEntity>();
        Assert.Null(plan.DeletedAt);
        var now = DateTimeOffset.UtcNow;
        plan.Delete(now);
        Assert.Equal(now, plan.DeletedAt);
        Assert.Throws<InvalidOperationException>(() => ProductionRules.EnsureCanEdit(plan, false));
        Assert.Throws<InvalidOperationException>(() => plan.CreateCorrection(now));
        Assert.Throws<InvalidOperationException>(() => plan.Delete(now));
    }

    [Fact]
    public void ReclassificationPreservesTimeWindow()
    {
        // Arrange: tạo downtime đã đóng và một lý do Breakdown mới dùng để phân loại lại.
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var end = DateTimeOffset.UtcNow;
        var downtime = DomainTestData.Create<DowntimeEventEntity>(("StartAt", start), ("EndAt", end));
        var reason = DomainTestData.Create<DowntimeReasonEntity>(("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown));
        // Act: chỉ thay đổi lý do/phân loại, không dịch chuyển khoảng thời gian thực tế của sự kiện.
        downtime.Reclassify(reason);
        // Assert: cửa sổ thời gian được giữ nguyên và reason mới được áp dụng.
        Assert.Equal(start, downtime.StartAt);
        Assert.Equal(end, downtime.EndAt);
        Assert.Equal(reason.Id, downtime.DowntimeReasonId);
        Assert.Equal(DowntimeKindEnum.Breakdown, downtime.Kind);
    }

    [Fact]
    public void DeviceTargetTakesPrecedenceOverSiteTarget()
    {
        // Arrange: cùng kỳ có target cấp Site và target riêng cho WorkUnit.
        var workUnitId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var site = DomainTestData.Create<OeeTargetEntity>(("SiteId", siteId), ("PeriodType", OeePeriodTypeEnum.Day), ("EffectiveFrom", new DateOnly(2026, 1, 1)), ("MinimumOee", 0.8m));
        var device = DomainTestData.Create<OeeTargetEntity>(("WorkUnitId", workUnitId), ("SiteId", siteId), ("PeriodType", OeePeriodTypeEnum.Day), ("EffectiveFrom", new DateOnly(2026, 1, 1)), ("MinimumOee", 0.9m));
        // Act & Assert: cấu hình thiết bị cụ thể phải được chọn trước cấu hình cấp nhà máy.
        Assert.Same(device, OeeTargetSelector.Select([site, device], workUnitId, siteId, new DateOnly(2026, 8, 1), OeePeriodTypeEnum.Day));
    }

    [Fact]
    public void TargetSelectionUsesDeviceThenSiteThenGlobalAndSpecificShift()
    {
        // Arrange: chuẩn bị target toàn cục, cấp Site, theo ca và theo thiết bị.
        var workUnitId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 2);
        var global = DomainTestData.Create<OeeTargetEntity>(("PeriodType", OeePeriodTypeEnum.Shift), ("PeriodCode", "SHIFT"), ("EffectiveFrom", date), ("MinimumOee", .70m));
        var site = DomainTestData.Create<OeeTargetEntity>(("SiteId", siteId), ("PeriodType", OeePeriodTypeEnum.Shift), ("PeriodCode", "SHIFT"), ("EffectiveFrom", date), ("MinimumOee", .75m));
        var siteS2 = DomainTestData.Create<OeeTargetEntity>(("SiteId", siteId), ("PeriodType", OeePeriodTypeEnum.Shift), ("PeriodCode", "S2"), ("EffectiveFrom", date), ("MinimumOee", .80m));
        var deviceS2 = DomainTestData.Create<OeeTargetEntity>(("WorkUnitId", workUnitId), ("PeriodType", OeePeriodTypeEnum.Shift), ("PeriodCode", "S2"), ("EffectiveFrom", date), ("MinimumOee", .85m));

        // Act & Assert: selector ưu tiên scope cụ thể nhất, rồi lùi về Site và cuối cùng là global.
        Assert.Same(deviceS2, OeeTargetSelector.Select([global, site, siteS2, deviceS2], workUnitId, siteId, date,
            OeePeriodTypeEnum.Shift, "SHIFT-S2-20260802"));
        Assert.Same(site, OeeTargetSelector.Select([global, site], workUnitId, siteId, date,
            OeePeriodTypeEnum.Shift, "SHIFT-S3-20260802"));
        Assert.Same(global, OeeTargetSelector.Select([global], workUnitId, siteId, date,
            OeePeriodTypeEnum.Shift, "SHIFT-S3-20260802"));
    }

    [Fact]
    public void AlertLifecycleEnforcesTransitionsAndCapturesAuthenticatedActor()
    {
        // Arrange: cảnh báo mới mở và thời điểm thao tác có offset local để kiểm tra chuẩn hóa UTC.
        var alert = DomainTestData.Create<OeeAlertEntity>(("State", AlertStateEnum.Open));
        var at = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.FromHours(7));
        // Act & Assert: acknowledge lưu actor, chuẩn hóa thời gian và không cho lặp transition sai trạng thái.
        alert.Acknowledge(at, "operator", "đang xử lý");
        Assert.Equal(AlertStateEnum.Acknowledged, alert.State);
        Assert.Equal("operator", alert.AcknowledgedBy);
        Assert.Equal(TimeSpan.Zero, alert.AcknowledgedAt!.Value.Offset);
        Assert.Throws<InvalidOperationException>(() => alert.Acknowledge(at, "operator", null));
        // Act & Assert: resolve hoàn tất vòng đời và tiếp tục chặn transition trùng.
        alert.Resolve(at.AddMinutes(5), "operator", "đã khắc phục");
        Assert.Equal(AlertStateEnum.Resolved, alert.State);
        Assert.Equal("đã khắc phục", alert.Note);
        Assert.Throws<InvalidOperationException>(() => alert.Resolve(at, "operator", null));
    }

    [Fact]
    public void OutputValidationPreventsOverlapShapedInvalidWindowAndBadDefects()
    {
        // Arrange: cùng một mốc thời gian được dùng để tạo cửa sổ có độ dài bằng 0.
        var now = DateTimeOffset.UtcNow;
        // Act & Assert: time window không hợp lệ và Defect lớn hơn Total đều bị chặn tại Domain.
        Assert.Throws<ArgumentException>(() => ProductionRules.ValidateOutput(1, 0, now, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductionRules.ValidateOutput(1, 2, now, now.AddMinutes(1)));
    }

    [Fact]
    public void AlertEvaluationUsesTargetAndSeverityThreshold()
    {
        // Arrange: Availability/OEE dưới ngưỡng, còn Performance/Quality đạt ngưỡng.
        var result = new OeeCalculationResultValue(60, 60, 0, 60, 100, 0.7m, 0.95m, 0.99m,
            0.65835m, null, null, [], "HASH");
        var target = DomainTestData.Create<OeeTargetEntity>(("MinimumAvailability", 0.9m), ("MinimumPerformance", 0.9m), ("MinimumQuality", 0.95m), ("MinimumOee", 0.85m));
        // Act: evaluator so sánh từng metric với target đang có hiệu lực.
        var alerts = AlertEvaluator.Evaluate(result, target);
        // Assert: chỉ phát cảnh báo cho chỉ số vi phạm và gán severity theo mức thiếu hụt.
        Assert.Contains(alerts, alert => alert.Metric == AlertMetricEnum.Availability && alert.Severity == AlertSeverityEnum.Critical);
        Assert.Contains(alerts, alert => alert.Metric == AlertMetricEnum.Oee);
        Assert.DoesNotContain(alerts, alert => alert.Metric == AlertMetricEnum.Performance);
    }
}
