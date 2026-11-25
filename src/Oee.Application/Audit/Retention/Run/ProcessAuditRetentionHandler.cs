using System.Text.Json;
using Oee.Application.Common;
using Oee.Domain.Platform;

namespace Oee.Application.Audit.Retention.Run;

/// <summary>Điều phối thao tác ProcessAuditRetention trong cùng scope lưu dữ liệu.</summary>
internal sealed class ProcessAuditRetentionHandler(
    IAuditRetentionStore store,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    AuditRetentionOptions options)
    : IRequestHandler<ProcessAuditRetentionCommand, (int DeletedCount, DateTimeOffset Cutoff)>
{
    public async Task<(int DeletedCount, DateTimeOffset Cutoff)> HandleAsync(
        ProcessAuditRetentionCommand command,
        CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow.AddMonths(-options.Months);
        var deleted = await store.DeleteBeforeAsync(cutoff, cancellationToken);

        if (deleted == 0)
            return (0, cutoff);
        unitOfWork.AddAudit(AuditLogEntity.Create(
                timestamp: clock.UtcNow,
                action: "RetentionCleanup",
                entityType: "AuditLog",
                entityId: "retention",
                userName: "System",
                entitySubject: $"Audit records before {cutoff:O}",
                changes: JsonSerializer.Serialize(new
                    {
                        DeletedCount = deleted,
                        Cutoff = cutoff
                    })));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (deleted, cutoff);
    }
}
