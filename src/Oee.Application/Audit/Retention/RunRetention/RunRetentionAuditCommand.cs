using Oee.Application.Common;
using Oee.Application.Contracts.Audit;

namespace Oee.Application.Audit.Retention.RunRetention;

/// <summary>Dữ liệu yêu cầu cho thao tác RunRetentionAudit.</summary>
public sealed record RunRetentionAuditCommand(AuditRetentionRequest Request) : ICommand<AuditRetentionResult>;
