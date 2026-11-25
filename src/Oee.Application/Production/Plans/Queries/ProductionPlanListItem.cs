using Oee.Domain.Common;

namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng plan đã projection tên quan hệ và RowVersion, không expose EF navigation.</summary>
/// <remarks>
/// ExecutionStatus là tiến độ hiển thị, tính tại thời điểm query từ khung giờ và tổng sản lượng.
/// Danh sách chỉ trả kế hoạch chưa xóa và chưa được thay thế bằng phiên bản khác.
/// </remarks>
public sealed record ProductionPlanListItem(
    Guid Id,
    string Code,
    Guid WorkUnitId,
    string WorkUnit,
    Guid ProductId,
    string Product,
    Guid? ShiftId,
    string? Shift,
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal PlannedQuantity,
    decimal ActualQuantity,
    decimal? Attainment,
    int Version,
    string RowVersion,
    bool HasOutput,
    ProductionPlanExecutionStatusEnum ExecutionStatus)
{
    public string? WorkUnitName { get; init; }
    public string? ProductName { get; init; }
}
