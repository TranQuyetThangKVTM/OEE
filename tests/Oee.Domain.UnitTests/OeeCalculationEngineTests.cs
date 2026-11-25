using Oee.Domain.Common;
using Oee.Domain.Oee;
using Oee.Domain.Organization;

namespace Oee.Domain.UnitTests;

/// <summary>
/// Các Unit Test thuần cho công thức OEE và bộ phân giải kỳ ca; nhóm test này không cần EF Core hay SQL Server.
/// </summary>
/// <remarks>
/// Kết quả A/P/Q/OEE được lưu theo tỷ lệ 0..1. Các test còn bảo vệ quy tắc không trả NaN/Infinity,
/// không đếm trùng downtime và chỉ dùng sự cố Failure/Breakdown khi tính MTBF/MTTR.
/// </remarks>
public sealed class OeeCalculationEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculatesApprovedFormulaAndFailureMetrics()
    {
        // Arrange & Act: tính OEE cho một ca tám giờ có thời gian ngoài kế hoạch và một sự cố Breakdown.
        var result = OeeCalculationEngine.Calculate(new OeeCalculationInputValue(
            Start, Start.AddHours(8), 60, 1800, 90, 0.2m,
            [new(Start.AddHours(1), Start.AddHours(2), DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown)]));

        // Assert: xác nhận từng thành phần công thức cùng MTBF/MTTR để phát hiện sai lệch nghiệp vụ sớm.
        Assert.Equal(420, result.LoadingMinutes);
        Assert.Equal(360, result.UtilizationMinutes);
        Assert.Equal(6m / 7m, result.Availability);
        Assert.Equal(1m, result.Performance);
        Assert.Equal(0.95m, result.Quality);
        Assert.Equal((6m / 7m) * 0.95m, result.Oee);
        Assert.Equal(360, result.MtbfMinutes);
        Assert.Equal(60, result.MttrMinutes);
    }

    [Fact]
    public void ZeroDenominatorsReturnNotApplicableWithoutNan()
    {
        // Arrange & Act: không có sản lượng và Loading time bằng 0 nên các mẫu số không hợp lệ.
        var result = OeeCalculationEngine.Calculate(new OeeCalculationInputValue(
            Start, Start.AddHours(1), 60, 0, 0, 0.1m, []));

        // Assert: null biểu diễn "không áp dụng", tránh NaN/Infinity đi vào database hoặc UI.
        Assert.Null(result.Availability);
        Assert.Null(result.Performance);
        Assert.Null(result.Quality);
        Assert.Null(result.Oee);
    }

    [Fact]
    public void RejectsDefectsGreaterThanTotal()
    {
        // Arrange, Act & Assert: Defect lớn hơn Total là dữ liệu sản xuất bất hợp lệ và phải bị từ chối.
        Assert.Throws<ArgumentOutOfRangeException>(() => OeeCalculationEngine.Calculate(
            new OeeCalculationInputValue(Start, Start.AddHours(1), 0, 9, 10, 1, [])));
    }

    [Fact]
    public void DuplicateAndOverlapAreNotDoubleCounted()
    {
        // Arrange: tạo một event lặp nguyên bản và một event chồng thời gian với nó.
        var first = new OeeDowntimeInputValue(Start, Start.AddMinutes(20), DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.MinorStop);

        // Act: engine hợp nhất cửa sổ downtime trước khi trừ vào thời gian chạy.
        var result = OeeCalculationEngine.Calculate(new OeeCalculationInputValue(
            Start, Start.AddHours(1), 0, 1, 0, 1, [first, first, first with { StartAt = Start.AddMinutes(10), EndAt = Start.AddMinutes(30) }]));

        // Assert: chỉ tính 30 phút duy nhất và vẫn gắn cờ bất thường để phục vụ kiểm tra chất lượng dữ liệu.
        Assert.Equal(30, result.AvailabilityLossMinutes);
        Assert.Contains("DuplicateDowntime", result.AnomalyFlags);
        Assert.Contains("OverlappingDowntime", result.AnomalyFlags);
    }

    [Fact]
    public void NonFailureDowntimeDoesNotAffectMtbfOrMttr()
    {
        // Arrange & Act: downtime Planned thuộc tổn thất Availability nhưng không phải sự cố hỏng máy.
        var result = OeeCalculationEngine.Calculate(new OeeCalculationInputValue(Start, Start.AddHours(1), 0, 1, 0, 1,
            [new(Start, Start.AddMinutes(10), DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Planned)]));

        // Assert: MTBF/MTTR không được tính từ setup, bảo trì kế hoạch hay dừng có kế hoạch.
        Assert.Null(result.MtbfMinutes);
        Assert.Null(result.MttrMinutes);
    }

    [Fact]
    public void InputHashIsDeterministic()
    {
        // Arrange: cùng một tập đầu vào phải luôn đại diện cho cùng một lần tính toán logic.
        var input = new OeeCalculationInputValue(Start, Start.AddHours(1), 0, 10, 1, 1, []);
        // Act & Assert: hash ổn định là nền tảng để recalculation snapshot có tính idempotent.
        Assert.Equal(OeeCalculationEngine.Calculate(input).InputHash, OeeCalculationEngine.Calculate(input).InputHash);
    }

    [Fact]
    public void ReclassificationChangesHashEvenWhenTimesAndFailureKindStayTheSame()
    {
        var stop = new OeeDowntimeInputValue(Start, Start.AddMinutes(10), DowntimeCategoryEnum.Unknown, DowntimeKindEnum.Breakdown);
        var input = new OeeCalculationInputValue(Start, Start.AddHours(1), 0, 500, 0, .1m, [stop]);
        var before = OeeCalculationEngine.Calculate(input);
        var after = OeeCalculationEngine.Calculate(input with { Downtimes = [stop with { Category = DowntimeCategoryEnum.AvailabilityLoss }] });
        Assert.NotEqual(before.InputHash, after.InputHash);
        Assert.Equal(60, before.UtilizationMinutes);
        Assert.Equal(50, after.UtilizationMinutes);
        Assert.Equal(50, before.MtbfMinutes);
        Assert.Equal(50, after.MtbfMinutes);
    }

    [Fact]
    public void ResolvesCrossMidnightShift()
    {
        // Arrange & Act: phân giải ca đêm 22:00 hôm trước đến 06:00 hôm sau trong múi giờ UTC.
        var period = ShiftPeriodResolver.Resolve(new DateOnly(2026, 8, 1), new TimeOnly(22, 0), new TimeOnly(6, 0), TimeZoneInfo.Utc);
        // Assert: ca kéo dài đúng tám giờ và vẫn mang ngày nghiệp vụ lúc bắt đầu ca.
        Assert.Equal(TimeSpan.FromHours(8), period.End - period.Start);
        Assert.Equal(new DateOnly(2026, 8, 1), period.LocalDate);
    }

    [Fact]
    public void RejectsShiftBoundaryInsideDstGap()
    {
        // Arrange: tại ngày chuyển DST, 02:30 không tồn tại trong múi giờ Eastern.
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        // Act & Assert: từ chối mốc local không tồn tại thay vì âm thầm quy đổi sai kỳ ca.
        Assert.Throws<ArgumentException>(() => ShiftPeriodResolver.Resolve(
            new DateOnly(2026, 3, 8), new TimeOnly(2, 30), new TimeOnly(6, 0), eastern));
    }
}
