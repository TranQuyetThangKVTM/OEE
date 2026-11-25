using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration;

/// <summary>Trạng thái OPC gọn dùng cho chỉ báo toàn cục trên thanh tiêu đề.</summary>
/// <remarks>WorstState là ConnectivityState, không phải trạng thái Running/Stopped.</remarks>
public sealed record OpcUiStatus(
    int Configured,
    int Connected,
    int Unhealthy,
    ConnectivityStateEnum WorstState,
    DateTimeOffset CheckedAt);
