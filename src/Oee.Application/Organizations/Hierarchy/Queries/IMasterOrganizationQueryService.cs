using Oee.Application.Contracts.Modules;

namespace Oee.Application.Modules;

/// <summary>Query tổ chức dạng danh sách gọn cho các màn hình tra cứu chung.</summary>
public interface IMasterOrganizationQueryService
{
    /// <summary>Đọc loại master/tổ chức được chỉ định bởi key mà implementation hỗ trợ.</summary>
    Task<IReadOnlyCollection<MasterRow>> GetAsync(string kind, CancellationToken cancellationToken);
}
