using Oee.Domain.Common;

namespace Oee.Domain.PlcIntegration;

/// <summary>Mapping động giữa một TagRole nghiệp vụ và NodeId OPC UA.</summary>
/// <remarks>Thiết kế dạng hàng giúp thêm role/tag mà không phải thêm cột schema cho mỗi NodeId.</remarks>
public sealed class OpcUaTagMappingEntity : SoftDeletableAuditedEntity
{
    public Guid PlcDeviceMappingId { get; private set; }
    public PlcDeviceMappingEntity PlcDeviceMapping { get; private set; } = null!;
    public TagRoleEnum TagRole { get; private set; }
    public TagDirectionEnum Direction { get; private set; }
    public string NodeId { get; private set; } = string.Empty;
    public OpcUaDataTypeEnum DataType { get; private set; }
    public bool IsRequired { get; private set; }

    private OpcUaTagMappingEntity()
    {
    }

    public static OpcUaTagMappingEntity Create(Guid plcDeviceMappingId, TagRoleEnum tagRole, TagDirectionEnum direction, string nodeId, OpcUaDataTypeEnum dataType, bool isRequired)
    {
        DomainGuard.Defined(tagRole);
        DomainGuard.Defined(direction);
        if ((direction == TagDirectionEnum.Read && tagRole is not (TagRoleEnum.Running or TagRoleEnum.TotalCounter or TagRoleEnum.DefectCounter or TagRoleEnum.FaultCode or TagRoleEnum.ProductCode))
            || (direction == TagDirectionEnum.Write && tagRole is TagRoleEnum.Running or TagRoleEnum.TotalCounter or TagRoleEnum.DefectCounter or TagRoleEnum.FaultCode or TagRoleEnum.ProductCode))
            throw new ArgumentException("Tag role is not valid for its direction.");
        ValidateDataType(tagRole, dataType);
        return new OpcUaTagMappingEntity
        {
            PlcDeviceMappingId = DomainGuard.RequiredId(plcDeviceMappingId),
            TagRole = tagRole,
            Direction = direction,
            NodeId = DomainGuard.Required(nodeId, 512),
            DataType = dataType,
            IsRequired = isRequired || tagRole is TagRoleEnum.Running or TagRoleEnum.TotalCounter
        };
    }

    public void Update(string nodeId, OpcUaDataTypeEnum dataType, bool isRequired)
    {
        var normalized = DomainGuard.Required(nodeId, 512);
        ValidateDataType(TagRole, dataType);
        NodeId = normalized;
        DataType = dataType;
        IsRequired = isRequired || TagRole is TagRoleEnum.Running or TagRoleEnum.TotalCounter;
    }

    private static void ValidateDataType(TagRoleEnum tagRole, OpcUaDataTypeEnum dataType)
    {
        DomainGuard.Defined(dataType);
        if (tagRole == TagRoleEnum.Running && dataType != OpcUaDataTypeEnum.BooleanValue)
            throw new ArgumentException("RunState must use BooleanValue.");
    }
}
