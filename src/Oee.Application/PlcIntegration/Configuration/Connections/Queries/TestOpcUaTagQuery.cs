using Oee.Application.Common;
using Oee.Application.PlcIntegration.Monitoring;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Queries;

/// <summary>Đọc thử NodeId đang sửa trước khi lưu mapping; không cập nhật checkpoint hoặc sản lượng.</summary>
public sealed record TestOpcUaTagQuery(Guid ConnectionId, string NodeId, string Role) : IRequest<OpcTagDirectRead>;
