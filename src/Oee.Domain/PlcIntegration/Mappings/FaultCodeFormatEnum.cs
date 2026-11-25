namespace Oee.Domain.Common;

/// <summary>Cách diễn giải fault code thô nhận từ PLC.</summary>
public enum FaultCodeFormatEnum
{
    Auto = 1,
    NumericDecimal = 2,
    Hex = 3,
    Text = 4
}
