using Oee.Application.Common;

namespace Oee.Infrastructure.Operations;

/// <summary>Implementation production của IClock, luôn trả thời điểm hiện tại theo UTC.</summary>
public sealed class SystemClock : IClock
{
    /// <summary>Thời điểm UTC tại lúc getter được đọc; không dùng giờ local của máy chủ.</summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
