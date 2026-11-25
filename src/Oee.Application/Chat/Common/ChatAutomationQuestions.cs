namespace Oee.Application.Chat;

/// <summary>Chỉ dẫn mở popup; câu chữ chat không tự ghi hoặc kích hoạt lịch.</summary>
internal static class ChatAutomationQuestions
{
    internal static bool Matches(string question)
    {
        var text = ChatIntentParser.Normalize(question);

        return (text.Contains("tu dong", StringComparison.Ordinal)
            && (text.Contains("ke hoach", StringComparison.Ordinal) || text.Contains("pdf", StringComparison.Ordinal)))
            || text.Contains("lich ke hoach", StringComparison.Ordinal)
            || text.Contains("pdf ca", StringComparison.Ordinal)
            || text.Contains("pdf theo ca", StringComparison.Ordinal);
    }

    internal static ChatAnswerDto Answer()
        => new(
            "Mở nút **Lịch kế hoạch / PDF ca** ở đầu cửa sổ chat.\nChọn kế hoạch mẫu, giờ tạo kế hoạch **ngày mai (D+1)** và bật lịch. Có thể bật riêng **PDF OEE sau mỗi ca**, chọn số phút chờ sau khi hết ca.\nHệ thống chạy nền trên IIS, bỏ qua kế hoạch trùng và không ghi đè kế hoạch xung đột. Mở lại popup để xem kết quả và tải PDF. Chức năng cần quyền quản lý kế hoạch; chỉ bắt đầu sau khi bạn bật và lưu.",
            [],
            ["Plan nào đang chạy?", "Trợ giúp"],
            ChatIntentKind.HowTo);
}
