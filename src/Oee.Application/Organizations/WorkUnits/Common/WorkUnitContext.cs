using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Organizations.WorkUnits.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác WorkUnit.</summary>
internal sealed class WorkUnitContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal Task EnsureUniqueWorkUnitCodeAsync(
        Guid parent,
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnyWorkUnitsAsync(x => x.WorkCenterId == parent && x.Code == code && x.Id != id, cancellationToken),
            "Work-unit code already exists in the group.");
}
