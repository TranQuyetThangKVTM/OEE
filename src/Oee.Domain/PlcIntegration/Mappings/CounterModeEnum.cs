namespace Oee.Domain.Common;

/// <summary>Quy ước reset counter của PLC để bộ xử lý tính delta đúng.</summary>
public enum CounterModeEnum
{
    Cumulative = 1,
    ResetPerShift = 2,
    ResetDaily = 3
}
