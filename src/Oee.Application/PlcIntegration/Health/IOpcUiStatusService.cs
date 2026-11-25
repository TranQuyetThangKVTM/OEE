namespace Oee.Application.PlcIntegration;

/// <summary>Query health OPC cho UI mà không làm UI phụ thuộc SDK OPC UA.</summary>
public interface IOpcUiStatusService
{
    /// <summary>Đọc snapshot trạng thái kết nối mới nhất.</summary>
    Task<OpcUiStatus> GetAsync(CancellationToken cancellationToken);
}
