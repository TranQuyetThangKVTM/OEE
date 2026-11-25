using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;

namespace Oee.Web.ErrorHandling;

/// <summary>Chuyển exception xuyên suốt Application/EF Core thành RFC 7807 ProblemDetails nhất quán.</summary>
public sealed partial class ApplicationExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApplicationExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Ánh xạ loại lỗi sang HTTP status và chỉ log stack trace cho lỗi 500 ngoài dự kiến.</summary>
    /// <remarks>
    /// 409 biểu thị rule nghiệp vụ, 412 biểu thị RowVersion/ETag cũ, 403 là thiếu quyền và 404 là không tìm thấy.
    /// CancellationToken được truyền vào ProblemDetails writer để request bị hủy không tiếp tục I/O.
    /// </remarks>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            AntiforgeryValidationException => StatusCodes.Status400BadRequest,
            ApplicationRuleException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            Oee.Application.Common.ConcurrencyConflictException or DbUpdateConcurrencyException => StatusCodes.Status412PreconditionFailed,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError,
        };
        if (status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, context.TraceIdentifier, exception);
        }

        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status == 500 ? "An unexpected error occurred." : exception.Message,
            },
            Exception = exception,
        });
    }

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "Unhandled request exception. TraceId: {TraceId}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string traceId,
        Exception exception);
}
