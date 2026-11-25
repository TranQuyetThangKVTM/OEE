using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Catalog.Products.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác Product.</summary>
internal sealed class ProductContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal static readonly HashSet<string> AllowedUoms = new(StringComparer.OrdinalIgnoreCase)
    {
        "PCS",
        "UNIT",
        "KG",
        "G",
        "L",
        "ML",
        "BOX",
        "CASE",
    };

    internal Task EnsureUniqueProductCodeAsync(
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnyProductsAsync(x => x.Code == code && x.Id != id, cancellationToken),
            "Product code already exists.");

    internal async Task<bool> ProductHasHistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await store.AnyProductionPlansAsync(x => x.ProductId == id, cancellationToken)
            || await store.AnyProductionOutputsAsync(x => x.ProductId == id, cancellationToken)
            || await store.AnySampleProductionPlanItemsAsync(x => x.ProductId == id, cancellationToken);
}
