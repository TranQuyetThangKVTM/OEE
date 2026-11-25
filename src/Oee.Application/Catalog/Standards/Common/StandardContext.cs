using Oee.Domain.Common;
using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Catalog.Standards.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác Standard.</summary>
internal sealed class StandardContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal async Task ValidateStandardReferencesAsync(
        Guid workUnitId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        await MasterDataCommandContext.EnsureActiveAsync(
            () => store.AnyWorkUnitsAsync(x => x.Id == workUnitId && x.Status == RecordStatusEnum.Active, cancellationToken),
            workUnitId,
            "Work unit");
        await MasterDataCommandContext.EnsureActiveAsync(
            () => store.AnyProductsAsync(x => x.Id == productId && x.Status == RecordStatusEnum.Active, cancellationToken),
            productId,
            "Product");
    }
}
