namespace Oee.Domain.Organization;

/// <summary>Chuyển ngày + giờ ca local thành biên thời gian có offset, hỗ trợ ca qua nửa đêm và DST.</summary>
public static class ShiftPeriodResolver
{
    /// <summary>Tạo khoảng ca cho một ngày vận hành local.</summary>
    /// <param name="localDate">Ngày mà ca bắt đầu theo lịch Site.</param>
    /// <param name="startTime">Giờ bắt đầu local.</param>
    /// <param name="endTime">Giờ kết thúc local; nhỏ hơn/bằng start nghĩa là ngày kế tiếp.</param>
    /// <param name="timeZone">Timezone của Site, không lấy tùy tiện timezone máy chủ.</param>
    /// <returns>Biên ca có offset và ngày local gốc.</returns>
    /// <exception cref="ArgumentException">Khi biên rơi vào khoảng local không tồn tại do DST.</exception>
    public static ShiftPeriodValue Resolve(DateOnly localDate, TimeOnly startTime, TimeOnly endTime, TimeZoneInfo timeZone)
    {
        // DateTimeKind.Unspecified rất quan trọng: đây là giờ trên đồng hồ Site, chưa phải UTC/local của host.
        var startLocal = localDate.ToDateTime(startTime, DateTimeKind.Unspecified);
        var endDate = endTime <= startTime ? localDate.AddDays(1) : localDate;
        var endLocal = endDate.ToDateTime(endTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(startLocal) || timeZone.IsInvalidTime(endLocal))
            throw new ArgumentException("Shift boundary falls in an invalid DST interval.");
        // Với giờ DST lặp lại, ResolveOffset chọn offset nhỏ hơn để kết quả nhất quán/idempotent.
        return new ShiftPeriodValue(new DateTimeOffset(startLocal, ResolveOffset(startLocal, timeZone)),
            new DateTimeOffset(endLocal, ResolveOffset(endLocal, timeZone)), localDate);
    }

    private static TimeSpan ResolveOffset(DateTime local, TimeZoneInfo zone) =>
        zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Min() : zone.GetUtcOffset(local);
}
