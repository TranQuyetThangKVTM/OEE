namespace Oee.Application.PlcIntegration;

// Các record private là dữ liệu đã được chuẩn hóa/validate. Tách chúng khỏi request giúp phần ghi Entity
// không phải lặp lại thao tác Trim, parse enum và kiểm tra giới hạn độ dài.
internal sealed record PlcConnectionValues(
    string Code,
    string Name,
    string Endpoint,
    string Mode,
    string Policy,
    string Auth,
    string? UserName,
    string? Secret,
    bool ClearSecret,
    string? Certificate,
    bool Active);
