using Oee.Application.Audit.Retention.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Audit;

namespace Oee.Application.Audit.Retention.PreviewRetention;

/// <summary>Điều phối thao tác PreviewRetentionAudit trong cùng scope lưu dữ liệu.</summary>
internal sealed class PreviewRetentionAuditHandler(AuditMaintenanceContext context)
    : IRequestHandler<PreviewRetentionAuditQuery, AuditRetentionPreview>
{
    public async Task<AuditRetentionPreview> HandleAsync(
        PreviewRetentionAuditQuery command,
        CancellationToken cancellationToken)
    {
        context.EnsureAuthorized();

        var cutoff = context.Clock.UtcNow.AddMonths(-context.Options.Months);
        var count = await context.Store.CountBeforeAsync(cutoff, cancellationToken);

        return new AuditRetentionPreview(cutoff, count, context.Options.Months);
    }
}
