using Oee.Application.Contracts.Modules;

namespace Oee.Application.Modules;

/// <summary>Đọc snapshot OEE/trạng thái mới nhất cho Live OEE.</summary>
public interface ILiveOeeQueryService
{
    /// <summary>Trả mô hình realtime hiện có tại thời điểm query, kèm timestamp.</summary>
    Task<LiveOeeModel> GetAsync(LiveOeeQuery query, CancellationToken cancellationToken);
}
