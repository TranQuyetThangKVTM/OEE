namespace Oee.Web.Components.Presentation;

/// <summary>Ý nghĩa màu trình bày thống nhất cho status/chip trong toàn ứng dụng.</summary>
/// <remarks>
/// Neutral là thông tin thường; Success là tốt/active; Warning cần chú ý; Error là lỗi/nguy hiểm;
/// Info là trạng thái cung cấp thông tin. Enum này không thay thế Domain status.
/// </remarks>
public enum SemanticKind { Neutral, Success, Warning, Error, Info }
