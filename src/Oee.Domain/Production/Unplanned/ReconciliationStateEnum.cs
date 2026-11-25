namespace Oee.Domain.Common;

/// <summary>Trạng thái xử lý dữ liệu PLC chưa đủ bằng chứng gắn ProductionPlan.</summary>
public enum ReconciliationStateEnum
{
    Pending = 1,
    Matched = 2,
    Rejected = 3
}
