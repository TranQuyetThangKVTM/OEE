using System.Runtime.CompilerServices;

namespace Oee.Domain.Common;

/// <summary>Kiểm tra và chuẩn hóa các bất biến giá trị dùng chung, không truy cập database hoặc framework.</summary>
public static class DomainGuard
{
    public static string Required(string? value,int maximumLength,[CallerArgumentExpression(nameof(value))] string parameter = "")
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{parameter} is required and cannot exceed {maximumLength} characters.",
                parameter);
        }

        return normalized;
    }

    public static string? Optional(string? value,int maximumLength,[CallerArgumentExpression(nameof(value))] string parameter = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{parameter} cannot exceed {maximumLength} characters.",
                parameter);
        }

        return normalized;
    }

    public static Guid RequiredId(Guid value,[CallerArgumentExpression(nameof(value))] string parameter = "")
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{parameter} is required.", parameter);
        }

        return value;
    }

    public static Guid? OptionalId(Guid? value,[CallerArgumentExpression(nameof(value))] string parameter = "")
    {
        return value.HasValue ? RequiredId(value.Value, parameter) : null;
    }

    public static T Defined<T>(T value,[CallerArgumentExpression(nameof(value))] string parameter = "")
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameter, "The enum value is invalid.");
        }

        return value;
    }

    public static decimal NonNegative(decimal value,[CallerArgumentExpression(nameof(value))] string parameter = "")
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameter, "The value cannot be negative.");
        }

        return value;
    }

    public static decimal Positive(decimal value,[CallerArgumentExpression(nameof(value))] string parameter = "")
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameter, "The value must be greater than zero.");
        }

        return value;
    }

    public static decimal Ratio(decimal value,[CallerArgumentExpression(nameof(value))] string parameter = "")
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(parameter, "The ratio must be between zero and one.");
        }

        return value;
    }

    public static void Period(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (endAt <= startAt)
        {
            throw new ArgumentException("Period end must be after period start.");
        }
    }

    public static void EffectiveRange(DateOnly from, DateOnly? until)
    {
        if (until < from)
        {
            throw new ArgumentException("EffectiveTo cannot be before EffectiveFrom.");
        }
    }
}
