using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;

namespace Oee.Application.Production.Plans.Correct;

/// <summary>Điều phối thao tác CorrectProductionPlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class CorrectProductionPlanHandler(ProductionPlanContext context)
    : IRequestHandler<CorrectProductionPlanCommand, ProductionPlanResponse>
{
    public async Task<ProductionPlanResponse> HandleAsync(
        CorrectProductionPlanCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var request = command.Request;
        var expectedRowVersion = command.ExpectedRowVersion;
        context.EnsureAuthorized();

        ProductionPlanValidator.Validate(request);
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var source = await context.FindAsync(id, cancellationToken);
        context.UnitOfWork.ExpectRowVersion(source, expectedRowVersion);
        await context.EnsureActiveWindowAsync(request, id, cancellationToken);
        // ReplacesPlanId loại bản nguồn khỏi kế hoạch sử dụng, vẫn giữ sản lượng gắn với bản gốc.
        var correction = source.CreateCorrection(context.Clock.UtcNow, request.Code);
        context.UnitOfWork.RequireUpdatedAtWrite(source);
        ProductionPlanContext.Apply(correction, request);
        context.Store.Add(correction);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ProductionPlanContext.Map(correction, false);
    }
}
