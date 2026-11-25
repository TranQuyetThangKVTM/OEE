using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Organizations.Areas.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác Area.</summary>
internal sealed class AreaContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal Task EnsureUniqueAreaCodeAsync(
        Guid parent,
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnyAreasAsync(x => x.SiteId == parent && x.Code == code && x.Id != id, cancellationToken),
            "Area code already exists in the site.");
}
