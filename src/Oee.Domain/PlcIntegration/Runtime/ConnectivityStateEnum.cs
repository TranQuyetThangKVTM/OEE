namespace Oee.Domain.Common;

/// <summary>Chất lượng kết nối/luồng dữ liệu PLC; không được dùng thay cho <see cref="OperationalStateEnum"/>.</summary>
public enum ConnectivityStateEnum
{
    NotConfigured = 0,
    NoData = 1,
    Connected = 2,
    Disconnected = 3,
    BadTag = 4
}
