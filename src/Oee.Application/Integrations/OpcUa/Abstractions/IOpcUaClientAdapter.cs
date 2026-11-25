namespace Oee.Application.PlcIntegration;

/// <summary>Factory kết nối OPC UA mà Worker sử dụng, không làm Application phụ thuộc SDK.</summary>
public interface IOpcUaClientAdapter
{
    /// <summary>Mở session có thể browse/read/write/subscribe.</summary>
    Task<IOpcUaSession> ConnectAsync(
        OpcUaEndpointOptions endpoint,
        CancellationToken cancellationToken);
}
