using System.Data;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Domain.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Common;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class PlcDeviceValidator
{
    internal static PlcDeviceValues ValidateDevice(PlcDeviceConfigurationRequest request)
    {
        if (request.ConnectionId == Guid.Empty || request.WorkUnitId == Guid.Empty)
            throw new ArgumentException("Connection and WorkUnit are required.");

        if (!Enum.TryParse<CounterModeEnum>(request.CounterMode, true, out var counterMode))
            throw new ArgumentException("Counter mode is invalid.");

        if (!Enum.TryParse<FaultCodeFormatEnum>(request.FaultCodeFormat, true, out var faultFormat))
            throw new ArgumentException("Fault-code format is invalid.");

        if (request.DebounceSamples is < 1 or > 20)
            throw new ArgumentException("Debounce samples must be between 1 and 20.");
        var tags = request.Tags.Select(x =>
            {
            // UI dùng tên RunState, còn Domain dùng Running; alias được đổi tại boundary này.
                var roleText = x.Role.Equals("RunState", StringComparison.OrdinalIgnoreCase) ? nameof(TagRoleEnum.Running) : x.Role;

                if (!Enum.TryParse<TagRoleEnum>(roleText, true, out var role))
                    throw new ArgumentException($"Tag role '{x.Role}' is invalid.");

                if (!Enum.TryParse<TagDirectionEnum>(x.Direction, true, out var direction))
                    throw new ArgumentException($"Tag direction '{x.Direction}' is invalid.");

                if (!Enum.TryParse<OpcUaDataTypeEnum>(x.DataType, true, out var dataType))
                    throw new ArgumentException($"Data type '{x.DataType}' is invalid.");

                if (direction == TagDirectionEnum.Read && !PlcDeviceContext.ReadRoles.Contains(role)
                    || direction == TagDirectionEnum.Write && !PlcDeviceContext.WriteRoles.Contains(role))
                    throw new ArgumentException($"Tag role '{x.Role}' is not valid for {direction}.");

                if (role == TagRoleEnum.Running && dataType != OpcUaDataTypeEnum.BooleanValue)
                    throw new ArgumentException("RunState must use BooleanValue.");

                return new PlcTagValues(
                    x.Id,
                    role,
                    direction,
                    Required(x.NodeId, 512, $"NodeId for {x.Role}"),
                    dataType,
                    role is TagRoleEnum.Running or TagRoleEnum.TotalCounter || x.IsRequired);
            })
            .ToArray();

        if (tags.GroupBy(x => (x.Role, x.Direction)).Any(x => x.Count() > 1))
            throw new ArgumentException("A tag role/direction can only be configured once.");

        // RunState và TotalCounter là hai input tối thiểu để state machine/counter ingestion hoạt động.
        if (tags.Length > 0 && (!tags.Any(x => x.Role == TagRoleEnum.Running && x.Direction == TagDirectionEnum.Read)
            || !tags.Any(x => x.Role == TagRoleEnum.TotalCounter && x.Direction == TagDirectionEnum.Read)))
            throw new ArgumentException("RunState and TotalCounter read tags are required.");

        return new(
            request.ConnectionId,
            request.WorkUnitId,
            request.DefaultProductId,
            Required(request.ExternalDeviceId, 128, "External device ID"),
            counterMode,
            faultFormat,
            request.ZeroFaultCodeMeansNoFault,
            request.DebounceSamples,
            Optional(request.Note, 500, "Note"),
            request.IsActive,
            tags);
    }
}
