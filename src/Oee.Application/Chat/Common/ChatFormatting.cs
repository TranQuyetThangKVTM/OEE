using System.Globalization;

namespace Oee.Application.Chat;

internal static class ChatFormatting
{
    internal static string Number(decimal? value)
        => value?.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "chưa có dữ liệu";

    internal static string Percent(decimal? value)
        => value?.ToString("P1", CultureInfo.GetCultureInfo("vi-VN")) ?? "chưa xác định";

    internal static ChatAnswerDto Answer(
        ChatIntentKind kind,
        string text,
        string label,
        string href)
        =>
        new(text, [new(label, href)], ChatSamples.Featured, kind);
}
