using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Common;
using Oee.Domain.Entities.Production;

namespace Oee.Application.Production.Samples.Create;

/// <summary>Điều phối thao tác CreateSamplePlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateSamplePlanHandler(SampleProductionPlanContext context)
    : IRequestHandler<CreateSamplePlanCommand, SamplePlanResponse>
{
    public async Task<SamplePlanResponse> HandleAsync(
        CreateSamplePlanCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        SampleProductionPlanValidator.Validate(request);

        var item = SampleProductionPlanEntity.Create(
            code: request.Code,
            name: request.Name,
            description: request.Description);

        context.Store.Add(item);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return SampleProductionPlanContext.Map(item);
    }
}
