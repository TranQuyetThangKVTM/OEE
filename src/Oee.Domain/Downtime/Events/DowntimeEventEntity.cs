using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;

namespace Oee.Domain.Downtime;

/// <summary>
/// Fact lịch sử mô tả một khoảng dừng của <see cref="WorkUnit"/> cùng lý do và nguồn ghi nhận.
/// </summary>
/// <remarks>
/// <see cref="EndAt"/> bằng <see langword="null"/> nghĩa là máy vẫn đang dừng. Event có thể liên kết một
/// <see cref="ProductionOutput"/> nhưng không bắt buộc. Thời điểm là <see cref="DateTimeOffset"/> để giữ offset/UTC;
/// việc hiển thị theo timezone Site do lớp ngoài xử lý. Entity không soft-delete vì là dữ liệu lịch sử.
/// </remarks>
public sealed class DowntimeEventEntity : AuditedEntity
{
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;

    public Guid? ProductionOutputId { get; private set; }
    public ProductionOutputEntity? ProductionOutput { get; private set; }

    public Guid DowntimeReasonId { get; private set; }
    public DowntimeReasonEntity DowntimeReason { get; private set; } = null!;

    public DowntimeCategoryEnum Category { get; private set; }
    public DowntimeKindEnum Kind { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset? EndAt { get; private set; }
    public DataSourceEnum Source { get; private set; }
    public string? Note { get; private set; }

    private DowntimeEventEntity()
    {
    }

    public static DowntimeEventEntity Create(Guid workUnitId, Guid downtimeReasonId, DowntimeCategoryEnum category, DowntimeKindEnum kind,
        DateTimeOffset startAt, DataSourceEnum source, DateTimeOffset? endAt = null, Guid? productionOutputId = null, string? note = null)
    {
        if (endAt.HasValue) DomainGuard.Period(startAt, endAt.Value);
        DowntimeReasonEntity.ValidateClassification(category, kind);
        return new DowntimeEventEntity
        {
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            DowntimeReasonId = DomainGuard.RequiredId(downtimeReasonId),
            Category = category,
            Kind = kind,
            StartAt = startAt.ToUniversalTime(),
            EndAt = endAt?.ToUniversalTime(),
            Source = DomainGuard.Defined(source),
            ProductionOutputId = DomainGuard.OptionalId(productionOutputId),
            Note = DomainGuard.Optional(note, 500)
        };
    }

    public void Close(DateTimeOffset endAt)
    {
        if (EndAt.HasValue) throw new InvalidOperationException("Downtime is already closed.");
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endAt, StartAt);
        EndAt = endAt.ToUniversalTime();
    }

    public void Reclassify(DowntimeReasonEntity reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        DowntimeReasonEntity.ValidateClassification(reason.Category, reason.Kind);
        DowntimeReasonId = reason.Id;
        DowntimeReason = reason;
        Category = reason.Category;
        Kind = reason.Kind;
    }
}
