using Oee.Application.Contracts.PlcIntegration;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Common;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class PlcFaultValidator
{
    internal static PlcFaultValues ValidateFault(PlcFaultMappingRequest request)
    {
        if (request.DowntimeReasonId == Guid.Empty)
            throw new ArgumentException("Downtime reason is required.");

        return new(
            request.WorkUnitId,
            Required(request.FaultCode, 64, "Fault code"),
            request.DowntimeReasonId,
            Optional(request.Description, 500, "Description"),
            request.IsActive);
    }
}
