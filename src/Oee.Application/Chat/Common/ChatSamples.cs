namespace Oee.Application.Chat;

public static class ChatSamples
{
    public static IReadOnlyList<string> Featured
    {
        get;
    } = Array.AsReadOnly(new[]
        {
            "Tóm tắt tình hình hôm nay",
            "Có máy nào đang dừng quá lâu không?",
            "Có cảnh báo nào đang mở?",
            "Plan nào đang chạy?",
            "Máy nào hiệu suất thấp hôm nay?",
            "Trợ giúp"
        });
}
