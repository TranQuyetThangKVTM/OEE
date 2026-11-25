using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Oee.Domain.Oee;

/// <summary>Tạo SHA-256 của dữ liệu tính OEE theo định dạng canonical đang dùng cho snapshot.</summary>
internal static class OeeInputHasher
{
    /// <summary>Băm input và các khoảng dừng đã chuẩn hóa, giữ nguyên version và thứ tự các trường JSON.</summary>
    public static string Compute(OeeCalculationInputValue input, List<OeeDowntimeInputValue> normalized)
    {
        // Chỉ đưa input ảnh hưởng công thức vào JSON canonical; timestamp được chuẩn hóa UTC.
        var canonical = JsonSerializer.Serialize(new
        {
            Version = OeeCalculationEngine.CalculationVersion,
            Start = input.PeriodStart.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            End = input.PeriodEnd.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            input.UnscheduledMinutes,
            input.TotalQuantity,
            input.DefectQuantity,
            input.IdealCycleMinutes,
            Downtimes = normalized.Select(item => new { Start = item.StartAt.ToUniversalTime(), End = item.EndAt.ToUniversalTime(), item.Category, item.Kind }),
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
