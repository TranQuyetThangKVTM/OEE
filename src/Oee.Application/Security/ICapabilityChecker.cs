namespace Oee.Application.Security;

/// <summary>Kiểm tra actor hiện tại có capability cụ thể hay không.</summary>
/// <remarks>Infrastructure/Web hiện thực việc đọc Claims; Application chỉ phụ thuộc abstraction này.</remarks>
public interface ICapabilityChecker
{
    /// <summary>Trả về <see langword="true"/> khi actor hiện tại được cấp capability.</summary>
    bool HasCapability(string capability);
}
