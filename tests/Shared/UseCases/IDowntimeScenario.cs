using Oee.Application.Contracts.Operations;

namespace Oee.Tests.UseCases;
/// <summary>Use case mở/đóng/phân loại downtime với EndAt nullable khi sự kiện còn mở.</summary>
public interface IDowntimeScenario
{
    /// <summary>Tạo sự kiện dừng; unique filtered index bảo vệ chỉ một event mở trên WorkUnit.</summary>
    Task<Guid> CreateAsync(DowntimeRequest request, CancellationToken cancellationToken);
    /// <summary>Đóng downtime tại endAt với optimistic concurrency.</summary>
    Task CloseAsync(Guid id, DateTimeOffset endAt, string concurrencyToken, CancellationToken cancellationToken);
    /// <summary>Đổi reason nhưng giữ nguyên time window và ghi audit.</summary>
    Task ReclassifyAsync(Guid id, Guid reasonId, string concurrencyToken, CancellationToken cancellationToken);
    /// <summary>Tái phân loại nhiều downtime theo một lệnh bulk.</summary>
    Task<int> BulkReclassifyAsync(IReadOnlyCollection<BulkReclassifyDowntimeItem> items, Guid reasonId, CancellationToken cancellationToken);
}
