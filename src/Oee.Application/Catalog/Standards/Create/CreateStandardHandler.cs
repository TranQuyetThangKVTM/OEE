using System.Data;
using Oee.Application.Catalog.Standards.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Standards.Create;

/// <summary>Điều phối thao tác CreateStandard trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateStandardHandler(StandardContext context)
    : IRequestHandler<CreateStandardCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateStandardCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateStandardValidator.ValidateStandard(command);
        await context.ValidateStandardReferencesAsync(values.WorkUnitId, values.ProductId, cancellationToken);
        await using var transaction = await context.Context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var laterOverlap = await context.store.AnyWorkUnitStandardsAsync(
            x => x.WorkUnitId == values.WorkUnitId && x.ProductId == values.ProductId && x.EffectiveFrom >= values.From
                && (!values.To.HasValue || x.EffectiveFrom <= values.To),
            cancellationToken);

        if (laterOverlap)
            throw new InvalidOperationException("The effective interval overlaps an existing standard version.");
        var previous = await context.store.FindPreviousStandardAsync(values.WorkUnitId, values.ProductId, values.From, cancellationToken);

        if (previous is not null)
        {
            previous.EndEffectiveness(values.From.AddDays(-1));
            context.Context.Touch(previous);
        }

        var entity = WorkUnitStandardEntity.Create(
            workUnitId: values.WorkUnitId,
            productId: values.ProductId,
            standardSpeedPerMinute: values.Speed,
            uom: values.Uom,
            effectiveFrom: values.From,
            effectiveTo: values.To,
            approvedBy: values.ApprovedBy);

        context.store.AddWorkUnitStandard(entity);
        await context.Context.SaveCreateAsync("Work-unit standard", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result(entity);
    }
}
