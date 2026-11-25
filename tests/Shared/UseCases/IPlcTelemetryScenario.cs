using Oee.Application.PlcIntegration;

namespace Oee.Tests.UseCases;
/// <summary>Use case xử lý telemetry PLC và điều phối counter/state/downtime/output.</summary>
public interface IPlcTelemetryScenario
{
    /// <summary>
    /// Xử lý một frame theo transaction/checkpoint; khi không có active plan, raw delta phải vào quarantine.
    /// </summary>
    Task<PlcProcessingResult> ProcessAsync(DeviceTelemetryFrame frame, CancellationToken cancellationToken);
}
