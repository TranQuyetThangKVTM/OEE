using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;
using Oee.Application.Production.Plans.Create;

namespace Oee.Application.Production.Plans.GenerateFromSample;

/// <summary>Điều phối thao tác GenerateProductionPlansFromSample trong cùng scope lưu dữ liệu.</summary>
internal sealed class GenerateProductionPlansFromSampleHandler(ProductionPlanContext context)
    : IRequestHandler<GenerateProductionPlansFromSampleCommand, int>
{
    public async Task<int> HandleAsync(
        GenerateProductionPlansFromSampleCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        context.EnsureAuthorized();

        if (request.ToDate < request.FromDate)
        {
            throw new ArgumentException("ToDate must not precede FromDate.");
        }

        var created = 0;

        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            var result = await context.Requests.ExecuteAsync(
                new CreateProductionPlansCommand(new BulkProductionPlanRequest(BulkPlanCreationModeContract.FromSample, [], [], null, request.SamplePlanId, date, null, null)),
                cancellationToken);
            created += result.CreatedCount;
        }

        return created;
    }
}
