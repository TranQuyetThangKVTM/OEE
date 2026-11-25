using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Organizations.Enterprises.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác Enterprise.</summary>
internal sealed class EnterpriseContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal Task EnsureUniqueEnterpriseCodeAsync(
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnyEnterprisesAsync(x => x.Code == code && x.Id != id, cancellationToken),
            "Enterprise code already exists.");
}
