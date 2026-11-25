using System.Text.Json;
using Oee.Application.Audit.Retention.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Audit;
using Oee.Domain.Platform;

namespace Oee.Application.Audit.Retention.RunRetention;

/// <summary>Điều phối thao tác RunRetentionAudit trong cùng scope lưu dữ liệu.</summary>
internal sealed class RunRetentionAuditHandler(AuditMaintenanceContext context)
    : IRequestHandler<RunRetentionAuditCommand, AuditRetentionResult>
{
    public async Task<AuditRetentionResult> HandleAsync(
        RunRetentionAuditCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        context.EnsureAuthorized();

        var startedAt = context.Clock.UtcNow;
        var cutoff = startedAt.AddMonths(-context.Options.Months);
        var candidateCount = await context.Store.CountBeforeAsync(cutoff, cancellationToken);
        var deletedCount = 0;
        const int batchSize = 1_000;
        // Lấy Id theo batch rồi yêu cầu adapter xóa qua luồng retention riêng của audit append-only.
        while (true)
        {
            var ids = await context.Store.ReadBatchIdsAsync(cutoff, batchSize, cancellationToken);

            if (ids.Length == 0)
            {
                break;
            }

            deletedCount += await context.Store.DeleteIdsAsync(ids, cancellationToken);
        }

        var completedAt = context.Clock.UtcNow;
        // Dấu vết nghiệp vụ và bảng history riêng chỉ được ghi sau khi biết DeletedCount thực tế.
        context.UnitOfWork.AddAudit(AuditLogEntity.Create(
                timestamp: completedAt,
                action: "RetentionCleanup",
                entityType: "AuditLog",
                entityId: "retention",
                userId: context.CurrentActor.UserId,
                userName: context.CurrentActor.UserName,
                entitySubject: $"Audit records before {cutoff:O}",
                changes: JsonSerializer.Serialize(new
                    {
                        DeletedCount = deletedCount,
                        Cutoff = cutoff,
                        RetentionMonths = context.Options.Months
                    }),
                ipAddress: AuditIpAddress.Normalize(context.CurrentActor.IpAddress)));
        context.Store.AddRun(AuditRetentionRunEntity.Create(
                startedAtUtc: startedAt,
                completedAtUtc: completedAt,
                cutoffUtc: cutoff,
                retentionMonths: context.Options.Months,
                candidateCount: candidateCount,
                deletedCount: deletedCount,
                actorUserName: context.CurrentActor.UserName,
                actorUserId: context.CurrentActor.UserId,
                ipAddress: AuditIpAddress.Normalize(context.CurrentActor.IpAddress)));
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return new AuditRetentionResult(cutoff, candidateCount, deletedCount, startedAt, completedAt);
    }
}
