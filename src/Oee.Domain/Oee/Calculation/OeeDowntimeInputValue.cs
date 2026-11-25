using Oee.Domain.Common;

namespace Oee.Domain.Oee;

/// <summary>Một downtime đóng đã được đưa vào kỳ tính OEE.</summary>
public sealed record OeeDowntimeInputValue(DateTimeOffset StartAt, DateTimeOffset EndAt, DowntimeCategoryEnum Category, DowntimeKindEnum Kind);
