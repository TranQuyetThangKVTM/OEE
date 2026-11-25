namespace Oee.Application.PlcIntegration;

/// <summary>Resolve secret tại runtime từ một tham chiếu cấu hình an toàn.</summary>
public interface ICredentialResolver
{
    /// <summary>Trả credential cho adapter; caller không được log giá trị kết quả.</summary>
    ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken);
}
