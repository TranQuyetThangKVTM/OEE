namespace Oee.Domain.Common;

/// <summary>Kiểu dữ liệu chuẩn hóa; giúp Application không phụ thuộc kiểu của OPC UA SDK.</summary>
public enum OpcUaDataTypeEnum
{
    BooleanValue = 1,
    Signed32 = 2,
    Unsigned32 = 3,
    Signed64 = 4,
    Unsigned64 = 5,
    SinglePrecision = 6,
    DoublePrecision = 7,
    Text = 8
}
