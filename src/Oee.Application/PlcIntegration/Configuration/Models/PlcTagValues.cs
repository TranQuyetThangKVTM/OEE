using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration;

internal sealed record PlcTagValues(
    Guid? Id,
    TagRoleEnum Role,
    TagDirectionEnum Direction,
    string NodeId,
    OpcUaDataTypeEnum DataType,
    bool Required);
