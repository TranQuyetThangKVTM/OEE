using System.Net;

namespace Oee.Application.Audit;

/// <summary>Chuẩn hóa địa chỉ IP cho audit, bỏ giá trị không hợp lệ.</summary>
public static class AuditIpAddress
{
    public static string? Normalize(string? value)
    {
        if (!IPAddress.TryParse(value, out var address))
            return null;

        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
    }
}
