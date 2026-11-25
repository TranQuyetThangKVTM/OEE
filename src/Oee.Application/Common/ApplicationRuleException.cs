namespace Oee.Application.Common;

/// <summary>
/// Báo một quy tắc use case không được thỏa mãn, ví dụ dữ liệu trùng hoặc trạng thái không cho phép thao tác.
/// </summary>
/// <remarks>
/// Presentation layer chuyển exception này thành lỗi nghiệp vụ thân thiện (hoặc ProblemDetails),
/// thay vì xem nó như lỗi hạ tầng. Lớp không tự ghi SQL hay log.
/// </remarks>
public sealed class ApplicationRuleException(string message) : Exception(message);
