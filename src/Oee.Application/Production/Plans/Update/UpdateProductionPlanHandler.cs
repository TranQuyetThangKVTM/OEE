using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;
using Oee.Domain.Production;

namespace Oee.Application.Production.Plans.Update;

/// <summary>Điều phối thao tác UpdateProductionPlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateProductionPlanHandler(ProductionPlanContext context)
    : IRequestHandler<UpdateProductionPlanCommand, ProductionPlanResponse>
{
    public async Task<ProductionPlanResponse> HandleAsync(
        UpdateProductionPlanCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var request = command.Request;
        var expectedRowVersion = command.ExpectedRowVersion;
        context.EnsureAuthorized();

        ProductionPlanValidator.Validate(request);
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var plan = await context.FindAsync(id, cancellationToken);
        // EF thêm RowVersion vào WHERE của UPDATE; 0 dòng bị ảnh hưởng sẽ thành concurrency exception.
        context.UnitOfWork.ExpectRowVersion(plan, expectedRowVersion);
        ProductionRules.EnsureCanEdit(plan, await context.Store.HasOutputAsync(id, cancellationToken));
        await context.EnsureActiveWindowAsync(request, id, cancellationToken);
        ProductionPlanContext.Apply(plan, request);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ProductionPlanContext.Map(plan, false);
    }
}
