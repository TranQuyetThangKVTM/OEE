using Oee.Application.Catalog.Products.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using static Oee.Application.Operations.MasterDataValidation;
using Oee.Application.Catalog.Products.Create;

namespace Oee.Application.Catalog.Products.Update;

/// <summary>Điều phối thao tác UpdateProduct trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateProductHandler(ProductContext context)
    : IRequestHandler<UpdateProductCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindProductAsync(command.Id, cancellationToken) ?? throw Missing("Product");
        var name = Required(command.Name, 200, "Product name");
        var uom = CreateProductValidator.ValidateUom(command.DefaultUom);

        if (!string.Equals(entity.DefaultUom, uom, StringComparison.OrdinalIgnoreCase)
            && await context.ProductHasHistoryAsync(entity.Id, cancellationToken))
            throw new InvalidOperationException("The default UOM cannot be changed after the product has production history.");
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        entity.Update(name, uom, command.Description);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
