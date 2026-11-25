using Oee.Application.Catalog.Standards.Common;
using Oee.Application.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Standards.Delete;

/// <summary>Điều phối thao tác DeleteStandard trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteStandardHandler(StandardContext context) : ICommandHandler<DeleteStandardCommand>
{
    public async Task HandleAsync(DeleteStandardCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindWorkUnitStandardAsync(id, cancellationToken)
            ?? throw Missing("Work-unit standard");

        if (await context.store.AnyProductionOutputsAsync(
            x => x.WorkUnitId == entity.WorkUnitId && x.ProductId == entity.ProductId,
            cancellationToken)
            || await context.store.AnyProductionPlansAsync(
            x => x.WorkUnitId == entity.WorkUnitId && x.ProductId == entity.ProductId,
            cancellationToken))
            throw Referenced("Work-unit standard");
        context.Context.SoftDelete(entity, rowVersion);
        context.Context.AddSemanticAudit("Delete", entity, new
            {
                IsDeleted = false
            }, new
            {
                IsDeleted = true
            });
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
