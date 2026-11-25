using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;

namespace Oee.Application.Production.Plans.Create;

/// <summary>Điều phối thao tác CreateProductionPlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateProductionPlanHandler(ProductionPlanContext context)
    : IRequestHandler<CreateProductionPlanCommand, ProductionPlanResponse>
{
    public async Task<ProductionPlanResponse> HandleAsync(
        CreateProductionPlanCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        context.EnsureAuthorized();

        // VersionSeriesId trỏ về Id của chính plan đầu tiên để các correction sau cùng một chuỗi.
        ProductionPlanValidator.Validate(request);
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await context.EnsureActiveWindowAsync(request, null, cancellationToken);
        var plan = ProductionPlanContext.MapNew(request);
        context.Store.Add(plan);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ProductionPlanContext.Map(plan, false);
    }
}
