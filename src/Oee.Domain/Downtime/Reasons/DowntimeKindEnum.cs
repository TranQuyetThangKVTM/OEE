namespace Oee.Domain.Common;

/// <summary>Loại sự kiện downtime chi tiết; MTBF/MTTR chỉ lấy Failure và Breakdown.</summary>
public enum DowntimeKindEnum
{
    Unknown = 0,
    Planned = 1,
    Failure = 2,
    Breakdown = 3,
    MinorStop = 4,
    Quality = 5
}
