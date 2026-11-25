using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.WorkUnits.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkUnits.Update;

/// <summary>Điều phối thao tác UpdateWorkUnit trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateWorkUnitHandler(WorkUnitContext context)
    : IRequestHandler<UpdateWorkUnitCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateWorkUnitCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindWorkUnitAsync(command.Id, cancellationToken) ?? throw Missing("Work unit");
        var code = command.Code is null ? entity.Code : Required(command.Code, 32, "Code");
        await context.EnsureUniqueWorkUnitCodeAsync(entity.WorkCenterId, code, entity.Id, cancellationToken);
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        entity.Update(code, command.Name, command.Description);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
