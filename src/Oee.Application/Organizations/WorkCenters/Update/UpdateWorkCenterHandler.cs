using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.WorkCenters.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkCenters.Update;

/// <summary>Điều phối thao tác UpdateWorkCenter trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateWorkCenterHandler(WorkCenterContext context)
    : IRequestHandler<UpdateWorkCenterCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateWorkCenterCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindWorkCenterAsync(command.Id, cancellationToken) ?? throw Missing("Work center");
        var code = command.Code is null ? entity.Code : Required(command.Code, 32, "Code");
        await context.EnsureUniqueGroupCodeAsync(entity.AreaId, code, entity.Id, cancellationToken);
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        entity.Update(code, command.Name, command.Description);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
