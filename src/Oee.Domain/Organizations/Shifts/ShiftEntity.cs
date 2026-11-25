using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Ca làm việc theo giờ local của Site.</summary>
public sealed class ShiftEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public Guid SiteId { get; private set; }
    public SiteEntity Site { get; private set; } = null!;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private ShiftEntity()
    {
    }

    public static ShiftEntity Create(Guid siteId, string code, string name, TimeOnly startTime, TimeOnly endTime, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        ValidateTimes(startTime, endTime);
        return new ShiftEntity
        {
            SiteId = DomainGuard.RequiredId(siteId),
            Code = DomainGuard.Required(code, 32),
            Name = DomainGuard.Required(name, 100),
            StartTime = startTime,
            EndTime = endTime,
            Status = DomainGuard.Defined(status)
        };
    }

    public void Update(string name, TimeOnly startTime, TimeOnly endTime)
    {
        string normalizedName = DomainGuard.Required(name, 100);
        ValidateTimes(startTime, endTime);
        Name = normalizedName;
        StartTime = startTime;
        EndTime = endTime;
    }

    public void ChangeStatus(RecordStatusEnum status) => Status = DomainGuard.Defined(status);

    private static void ValidateTimes(TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime == endTime) throw new ArgumentException("Shift start/end must differ.");
    }
}
