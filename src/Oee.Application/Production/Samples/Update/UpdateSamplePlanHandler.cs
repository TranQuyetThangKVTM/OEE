using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Common;

namespace Oee.Application.Production.Samples.Update;

/// <summary>Điều phối thao tác UpdateSamplePlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateSamplePlanHandler(SampleProductionPlanContext context)
    : IRequestHandler<UpdateSamplePlanCommand, SamplePlanResponse>
{
    public async Task<SamplePlanResponse> HandleAsync(
        UpdateSamplePlanCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var request = command.Request;
        var expectedRowVersion = command.ExpectedRowVersion;
        SampleProductionPlanValidator.Validate(request);
        var item = await context.FindAsync(id, cancellationToken);
        context.UnitOfWork.ExpectRowVersion(item, expectedRowVersion);
        item.Update(request.Code, request.Name, request.Description);
        item.RecordUpdate(context.Clock.UtcNow);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return SampleProductionPlanContext.Map(item);
    }
}
