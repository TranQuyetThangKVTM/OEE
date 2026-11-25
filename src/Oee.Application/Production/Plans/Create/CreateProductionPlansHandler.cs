using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;

namespace Oee.Application.Production.Plans.Create;

/// <summary>Điều phối thao tác CreateProductionPlans trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateProductionPlansHandler(ProductionPlanContext context)
    : IRequestHandler<CreateProductionPlansCommand, BulkProductionPlanResult>
{
    public async Task<BulkProductionPlanResult> HandleAsync(
        CreateProductionPlansCommand command,
        CancellationToken cancellationToken)
    {
        context.EnsureAuthorized();

        return await context.CreateAsync(command.Request, cancellationToken);
    }
}
