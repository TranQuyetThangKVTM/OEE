using System.Globalization;

namespace Oee.Application.PlcIntegration.Monitoring;

/// <summary>Chuỗi chẩn đoán giới hạn kích thước, không ép kiểu counter hoặc thay đổi giá trị gốc.</summary>
public static class OpcTagValueFormatter
{
    public static string Format(object? value)
    {
        var text = value switch
        {
            null => "—",
            byte[] bytes => Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 128))) + (bytes.Length > 128 ? "…" : ""),
            Array array => "[" + string.Join(", ", array.Cast<object?>().Take(20).Select(Format)) + (array.Length > 20 ? ", …]" : "]"),
            bool flag => flag ? "true" : "false",
            decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
            IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        }
            ?? "—";

        return text.Length <= 512 ? text : text[..512] + "…";
    }
}
