namespace Oee.Application.Common;

/// <summary>
/// Abstraction cung cấp thời điểm UTC để use case không phụ thuộc trực tiếp vào đồng hồ máy chủ.
/// </summary>
/// <remarks>
/// Production dùng clock thật; test có thể cấp một thời điểm cố định. Nhờ vậy các tính toán ca,
/// hiệu lực và lookback có thể kiểm thử lặp lại mà không phụ thuộc thời điểm chạy test.
/// </remarks>
public interface IClock
{
    /// <summary>Thời điểm hiện tại có offset UTC.</summary>
    DateTimeOffset UtcNow
    {
        get;
    }
}
