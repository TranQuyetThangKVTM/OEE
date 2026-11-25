using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration;

/// <summary>Chuẩn hóa chuỗi, token và phân trang cho cấu hình PLC; không thực hiện I/O.</summary>
internal static class PlcConfigurationValidation
{
    internal static string RequiredVersion(string? value)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("RowVersion is required.") : value;

    internal static string Required(
        string? value,
        int max,
        string name) => DomainGuard.Required(value, max, name);

    internal static string? Optional(
        string? value,
        int max,
        string name) => DomainGuard.Optional(value, max, name);

    internal static KeyNotFoundException Missing(string name) => new($"{name} was not found.");
}
