using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Common;
using Oee.Domain.Entities.Production;

namespace Oee.Application.Production.Samples.Create;

/// <summary>Điều phối thao tác CreateSamplePlanWithItems trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateSamplePlanWithItemsHandler(SampleProductionPlanContext context)
    : IRequestHandler<CreateSamplePlanWithItemsCommand, SamplePlanBulkResult>
{
    public async Task<SamplePlanBulkResult> HandleAsync(
        CreateSamplePlanWithItemsCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        SampleProductionPlanValidator.ValidateBulk(
            request.Name,
            request.Description,
            request.WorkUnitIds,
            request.ShiftIds,
            request.ProductId,
            request.PlannedQuantity,
            request.Notes);
        // Serializable và unique key bảo vệ khỏi hai request cùng tạo một tổ hợp.
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var combinations = await context.ResolveActiveCombinationsAsync(request.WorkUnitIds, request.ShiftIds, request.ProductId, cancellationToken);

            var parent = SampleProductionPlanEntity.Create(
                code: $"SAMPLE-{Guid.CreateVersion7():N}"[..19].ToUpperInvariant(),
                name: request.Name,
                description: request.Description);

            // Tạo toàn bộ entity trong memory rồi AddRange; không SaveChanges từng dòng.
            var items = combinations.Select(x => SampleProductionPlanItemEntity.Create(
                    sampleProductionPlanId: parent.Id,
                    workUnitId: x.WorkUnitId,
                    productId: request.ProductId,
                    shiftId: x.ShiftId,
                    plannedQuantity: request.PlannedQuantity,
                    notes: request.Notes))
                .ToArray();
            // Parent, children và audit cùng một SaveChanges/transaction để rollback toàn bộ khi lỗi.
            context.Store.Add(parent);
            context.Store.AddItems(items);
            context.UnitOfWork.AddAudit(context.CreateBulkAudit("SampleProductionPlanCreated", parent.Id, combinations.Count, items.Length, 0));
            await context.UnitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SamplePlanBulkResult(
                parent.Id,
                combinations.Count,
                items.Length,
                0,
                items.Select(SampleProductionPlanContext.Map).ToArray(),
                []);
        }
        // Constraint race được đổi thành lỗi use case; transaction chưa commit sẽ rollback khi dispose.
        catch (PersistenceWriteException exception)
        {
            throw new InvalidOperationException(
                "Không thể tạo kế hoạch mẫu vì mã hoặc tổ hợp dòng bị trùng. Toàn bộ giao dịch đã được hoàn tác.",
                exception);
        }
    }
}
