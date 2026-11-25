using Oee.Application.Catalog.Products.Common;
using Oee.Application.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Products.Delete;

/// <summary>Điều phối thao tác DeleteProduct trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteProductHandler(ProductContext context) : ICommandHandler<DeleteProductCommand>
{
    public async Task HandleAsync(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindProductAsync(id, cancellationToken) ?? throw Missing("Product");

        if (await context.store.AnyProductionPlansAsync(x => x.ProductId == id, cancellationToken)
            || await context.store.AnyProductionOutputsAsync(x => x.ProductId == id, cancellationToken)
            || await context.store.AnyProductionReconciliationItemsAsync(x => x.ProductId == id, cancellationToken)
            || await context.store.AnySampleProductionPlanItemsAsync(x => x.ProductId == id, cancellationToken)
            || await context.store.AnyWorkUnitStandardsAsync(x => x.ProductId == id, cancellationToken)
            || await context.store.AnyPlcDeviceMappingsAsync(x => x.DefaultProductId == id, cancellationToken))
            throw Referenced("Product");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
