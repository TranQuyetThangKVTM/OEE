using System.Linq.Expressions;
using Oee.Domain.Entities.Oee;

namespace Oee.Application.Oee;

public interface IOeeTargetStore
{
    Task<OeeTargetEntity?> FindAsync(Guid id, CancellationToken ct);

    Task<OeeTargetEntity?> FindPreviousAsync(
        Expression<Func<OeeTargetEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyAsync(Expression<Func<OeeTargetEntity, bool>> predicate, CancellationToken ct);

    void Add(OeeTargetEntity target);
}
