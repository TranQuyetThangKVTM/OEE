using Oee.Application.Common;
using Oee.Application.Contracts.Audit;

namespace Oee.Application.Audit.Retention.PreviewRetention;

/// <summary>Dữ liệu yêu cầu cho thao tác PreviewRetentionAudit.</summary>
public sealed record PreviewRetentionAuditQuery() : IQuery<AuditRetentionPreview>;
