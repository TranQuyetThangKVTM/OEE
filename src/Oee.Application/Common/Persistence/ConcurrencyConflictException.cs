namespace Oee.Application.Common;

/// <summary>Dữ liệu đã thay đổi kể từ lúc người dùng đọc, không được ghi đè bằng token cũ.</summary>
public sealed class ConcurrencyConflictException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
