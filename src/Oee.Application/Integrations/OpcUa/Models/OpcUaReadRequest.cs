using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration;

/// <summary>Yêu cầu đọc NodeId với TagRole nghiệp vụ tương ứng.</summary>
public sealed record OpcUaReadRequest(
    string NodeId,
    TagRoleEnum Role);
