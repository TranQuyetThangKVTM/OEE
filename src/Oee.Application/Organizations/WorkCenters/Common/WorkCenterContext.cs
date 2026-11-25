using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Organizations.WorkCenters.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác WorkCenter.</summary>
internal sealed class WorkCenterContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal Task EnsureUniqueGroupCodeAsync(
        Guid parent,
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnyWorkCentersAsync(x => x.AreaId == parent && x.Code == code && x.Id != id, cancellationToken),
            "Work-unit-group code already exists in the area.");
}
