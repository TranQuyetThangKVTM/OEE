namespace Oee.Domain.Common;

/// <summary>Vai trò nghiệp vụ động của NodeId OPC UA.</summary>
public enum TagRoleEnum
{
    Running = 1,
    TotalCounter = 2,
    DefectCounter = 3,
    FaultCode = 4,
    ProductCode = 5,
    Oee = 10,
    Availability = 11,
    Performance = 12,
    Quality = 13,
    CurrentDowntime = 14,
    GoodQuantity = 15,
    TargetQuantity = 16,
    Mtbf = 17,
    Mttr = 18
}
