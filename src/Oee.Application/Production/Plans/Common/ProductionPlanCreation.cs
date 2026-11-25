using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Domain.Entities.Production;

namespace Oee.Application.Production.Plans.Common;

internal sealed partial class ProductionPlanContext
{
    internal async Task<BulkProductionPlanResult> CreateAsync(
        BulkProductionPlanRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var candidates = await BuildCandidatesAsync(request, cancellationToken);
            candidates = await MarkExistingAsync(candidates, cancellationToken);
            // Candidate Skipped/Conflict được trả về UI nhưng không được thêm vào ChangeTracker.
            var valid = candidates.Where(x => x.State == "Valid").ToArray();
            var reservedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var productCode in valid.Select(candidate => candidate.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                reservedCodes.UnionWith(await Store.ReadReservedCodesAsync(
                    FormattableString.Invariant($"{productCode}-{request.AppliedDate:yyyyMMdd}-"),
                    cancellationToken));
            }
            var plans = valid.Select(candidate =>
                {
                    var planId = Guid.CreateVersion7();

                    return ProductionPlanEntity.Create(
                        candidate.WorkUnitId,
                        candidate.ProductId,
                        ProductionPlanContext.BuildPlanCode(candidate, request.AppliedDate, reservedCodes),
                        candidate.StartAt,
                        candidate.EndAt,
                        candidate.PlannedQuantity,
                        candidate.ShiftId,
                        ProductionPlanContext.NullIfWhiteSpace(request.Notes) ?? candidate.Notes,
                        candidate.SourceSamplePlanId,
                        candidate.SourceSamplePlanItemId,
                        planId);
                })
                .ToArray();
            // Plans và audit bulk cùng SaveChanges nên rollback cùng nhau nếu transaction thất bại.
            Store.AddRange(plans);
            UnitOfWork.AddAudit(CreateBulkAudit(request, plans, candidates));
            await UnitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new BulkProductionPlanResult(
                candidates.Count,
                plans.Length,
                candidates.Count(x => x.State == "Skipped"),
                candidates.Count(x => x.State == "Conflict"),
                plans.Select(x => x.Id).ToArray(),
                candidates.Select(ProductionPlanContext.ToContract).ToArray());
        }
        // Unique/FK/trigger race từ SQL được chuyển thành thông báo nghiệp vụ cho UI tải lại preview.
        catch (PersistenceWriteException exception)
        {
            throw new InvalidOperationException(
                "Không thể tạo kế hoạch vì dữ liệu đã thay đổi hoặc một tổ hợp vừa được tạo bởi người dùng khác. Hãy tải lại bản xem trước.",
                exception);
        }
    }
}
