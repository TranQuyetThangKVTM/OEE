using Oee.Domain.Common;

namespace Oee.Tests.UseCases;
/// <summary>
/// Use case tính lại snapshot OEE theo thiết bị và period, được API quản trị hoặc Worker gọi.
/// </summary>
/// <remarks>
/// Implementation điều phối persistence port, gọi calculation engine thuần ở Domain rồi upsert snapshot
/// theo CalculationVersion/InputHash. Giao diện không tự tính lại công thức OEE.
/// </remarks>
public interface IOeeSnapshotScenario
{
    /// <summary>Tính lại một period cụ thể và trả Id snapshot đã tạo/cập nhật.</summary>
    Task<Guid> RecalculateAsync(Guid workUnitId, OeePeriodTypeEnum periodType, string periodCode, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken);
    /// <summary>Tính lại các period nằm trong cửa sổ lookback; Worker dùng để tự hiệu chỉnh dữ liệu đến muộn.</summary>
    Task<int> RecalculateLookbackAsync(TimeSpan lookback, CancellationToken cancellationToken);
}
