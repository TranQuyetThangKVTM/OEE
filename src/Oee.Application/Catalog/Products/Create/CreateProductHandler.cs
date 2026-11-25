using Oee.Application.Catalog.Products.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Products.Create;

/// <summary>Điều phối thao tác CreateProduct trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateProductHandler(ProductContext context)
    : IRequestHandler<CreateProductCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateProductValidator.ValidateProduct(command);
        await context.EnsureUniqueProductCodeAsync(values.Code, null, cancellationToken);

        var entity = ProductEntity.Create(
            code: values.Code,
            name: values.Name,
            defaultUom: values.Uom,
            description: values.Description,
            status: values.Status);

        context.store.AddProduct(entity);
        await context.Context.SaveCreateAsync("Product", cancellationToken);

        return Result(entity);
    }
}
