using Oee.Domain.Common;
using Oee.Application.Catalog.Products.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Products.SetProductStatus;

/// <summary>Điều phối thao tác SetProductStatus trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetProductStatusHandler(ProductContext context)
    : IRequestHandler<SetProductStatusCommand, MasterMutationResult>
{
    public Task<MasterMutationResult> HandleAsync(
        SetProductStatusCommand command,
        CancellationToken cancellationToken)
    {
        return context.Context.SetStatusAsync(
            token => context.store.FindProductAsync(command.Id, token),
            command.TargetStatus,
            command.RowVersion,
            "Product",
            async (entity, target) =>
            {
                if (target == RecordStatusEnum.Inactive
                    && await context.store.AnyWorkUnitStandardsAsync(x => x.ProductId == entity.Id, cancellationToken))
                    throw ActiveDependencies("Product", "work-unit standards");
            },
            cancellationToken);
    }
}
