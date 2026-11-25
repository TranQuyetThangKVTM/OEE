using System.Data;
using Oee.Domain.Common;
using Oee.Domain.Platform;

namespace Oee.Application.Common;

/// <summary>Đơn vị ghi dữ liệu dùng chung giữa các store trong một use case, không lộ EF hoặc SQL.</summary>
public interface IApplicationUnitOfWork
{
    bool HasChanges
    {
        get;
    }

    Task<IApplicationTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void ExpectRowVersion(SoftDeletableAuditedEntity entity, byte[] expected);

    void ExpectUpdatedAt(AuditedEntity entity, DateTimeOffset? expected);

    void RequireUpdatedAtWrite(AuditedEntity entity);

    void AddAudit(AuditLogEntity entry);

    void DiscardChanges();
}
