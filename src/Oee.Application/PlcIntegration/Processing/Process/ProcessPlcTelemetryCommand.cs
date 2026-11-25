using Oee.Application.Common;

namespace Oee.Application.PlcIntegration.Processing.Process;

/// <summary>Dữ liệu yêu cầu cho thao tác ProcessPlcTelemetry.</summary>
public sealed record ProcessPlcTelemetryCommand(DeviceTelemetryFrame Frame) : ICommand<PlcProcessingResult>;
