namespace Oee.Application;

/// <summary>
/// Đánh dấu assembly Application để composition root và Architecture Test có thể tham chiếu
/// đúng assembly mà không phải chọn một use case cụ thể.
/// </summary>
/// <remarks>
/// Type này không xử lý nghiệp vụ và không đọc/ghi SQL. Web/Worker dùng assembly chứa nó
/// khi đăng ký Dependency Injection hoặc quét các thành phần của tầng Application.
/// </remarks>
public static class ApplicationAssembly;
