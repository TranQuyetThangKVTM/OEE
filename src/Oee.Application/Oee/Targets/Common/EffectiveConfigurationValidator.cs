namespace Oee.Application.Oee.Targets.Common;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class EffectiveConfigurationValidator
{
    internal static void ValidateRatio(decimal value) => Domain.Common.DomainGuard.Ratio(value);
}
