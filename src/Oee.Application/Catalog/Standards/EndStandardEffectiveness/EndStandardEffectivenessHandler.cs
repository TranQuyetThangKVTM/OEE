using Oee.Application.Catalog.Standards.Common;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Standards.EndStandardEffectiveness;

/// <summary>Điều phối thao tác EndStandardEffectiveness trong cùng scope lưu dữ liệu.</summary>
internal sealed class EndStandardEffectivenessHandler(StandardContext context)
    : IRequestHandler<EndStandardEffectivenessCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        EndStandardEffectivenessCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindWorkUnitStandardAsync(command.Id, cancellationToken)
            ?? throw Missing("Work-unit standard");

        if (command.EffectiveTo < entity.EffectiveFrom)
            throw new ArgumentException("EffectiveTo cannot be before EffectiveFrom.");

        if (entity.EffectiveTo == command.EffectiveTo)
            return Result(entity);

        if (entity.EffectiveTo.HasValue)
            throw new InvalidOperationException("A historical standard cannot have its effective interval changed. Create a new version instead.");
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        var overlaps = await context.store.AnyWorkUnitStandardsAsync(
            x => x.Id != entity.Id && x.WorkUnitId == entity.WorkUnitId && x.ProductId == entity.ProductId
                && x.EffectiveFrom <= command.EffectiveTo
                && (!x.EffectiveTo.HasValue || x.EffectiveTo >= entity.EffectiveFrom),
            cancellationToken);

        if (overlaps)
            throw new InvalidOperationException("The effective interval overlaps another standard version.");
        var before = entity.EffectiveTo;
        entity.EndEffectiveness(command.EffectiveTo);
        context.Context.Touch(entity);
        context.Context.AddSemanticAudit(
            "EndEffectiveness",
            entity,
            new
            {
                EffectiveTo = before
            },
            new
            {
                entity.EffectiveTo
            });
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
