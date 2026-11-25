using System.ComponentModel.DataAnnotations;

namespace Oee.Web.Validation;

/// <summary>Endpoint filter chạy DataAnnotations server-side cho request DTO kiểu <typeparamref name="T"/>.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    /// <summary>Trả ValidationProblem theo từng field nếu DTO không hợp lệ; ngược lại gọi handler tiếp theo.</summary>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var value = context.Arguments.OfType<T>().FirstOrDefault();
        if (value is null)
        {
            return Results.BadRequest();
        }

        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(value, new ValidationContext(value), validationResults, true))
        {
            return await next(context);
        }

        // Gom nhiều lỗi của cùng một property theo format ValidationProblem chuẩn mà UI/mobile dễ xử lý.
        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (member, result.ErrorMessage ?? "Invalid value.")))
            .GroupBy(item => item.member, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Item2).ToArray(), StringComparer.Ordinal);
        return Results.ValidationProblem(errors);
    }
}
