using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.Integrations.OpcUa;

/// <summary>Đọc secret OPC UA gián tiếp từ biến môi trường theo tên reference đã cấu hình.</summary>
/// <remarks>Giá trị secret chỉ đi vào SDK khi tạo identity và không được trả về UI hay ghi log.</remarks>
public sealed class EnvironmentCredentialResolver : ICredentialResolver
{
    /// <summary>Resolve giá trị secret, đồng thời tôn trọng yêu cầu hủy của caller.</summary>
    /// <param name="secretReference">Tên biến môi trường, không phải mật khẩu rõ.</param>
    /// <param name="cancellationToken">Tín hiệu hủy của luồng kết nối.</param>
    /// <returns>Secret nếu biến môi trường tồn tại; ngược lại là null.</returns>
    public ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Environment.GetEnvironmentVariable(secretReference)); }
}
