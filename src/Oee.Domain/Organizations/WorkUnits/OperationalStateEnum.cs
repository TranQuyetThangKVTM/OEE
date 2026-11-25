namespace Oee.Domain.Common;

/// <summary>Trạng thái vận hành thực tế của máy; độc lập với trạng thái kết nối dữ liệu.</summary>
public enum OperationalStateEnum
{
    Unknown = 0,
    Running = 1,
    Stopped = 2,
    Setup = 3,
    Maintenance = 4,
    Cleaning = 5,
    Idle = 6
}
