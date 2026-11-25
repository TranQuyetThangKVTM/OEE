using Oee.Application.Common;
using Oee.Application.Production.Plans.Common;

namespace Oee.Application.Production.Plans.Delete;

/// <summary>Điều phối thao tác DeleteProductionPlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteProductionPlanHandler(ProductionPlanContext context)
    : ICommandHandler<DeleteProductionPlanCommand>
{
    public async Task HandleAsync(
        DeleteProductionPlanCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var expectedRowVersion = command.ExpectedRowVersion;
        context.EnsureAuthorized();

        var plan = await context.FindAsync(id, cancellationToken);

        if (!plan.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
            throw new ConcurrencyConflictException("Production plan has changed.");
        context.UnitOfWork.ExpectRowVersion(plan, expectedRowVersion);
        plan.Delete(context.Clock.UtcNow);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
