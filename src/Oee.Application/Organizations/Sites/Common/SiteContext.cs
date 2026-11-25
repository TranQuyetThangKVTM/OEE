using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Organizations.Sites.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác Site.</summary>
internal sealed class SiteContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal Task EnsureUniqueSiteCodeAsync(
        Guid parent,
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnySitesAsync(x => x.EnterpriseId == parent && x.Code == code && x.Id != id, cancellationToken),
            "Site code already exists in the enterprise.");
}
