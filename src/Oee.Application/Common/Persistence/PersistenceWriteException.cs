namespace Oee.Application.Common;

/// <summary>Lỗi ghi dữ liệu kỹ thuật được adapter chuyển sang contract trung lập database.</summary>
public sealed class PersistenceWriteException(
    string message,
    Exception innerException)
    : Exception(message, innerException);
