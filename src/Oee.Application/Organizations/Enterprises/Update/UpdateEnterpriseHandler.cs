using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.Enterprises.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Enterprises.Update;

/// <summary>Điều phối thao tác UpdateEnterprise trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateEnterpriseHandler(EnterpriseContext context)
    : IRequestHandler<UpdateEnterpriseCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateEnterpriseCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindEnterpriseAsync(command.Id, cancellationToken) ?? throw Missing("Enterprise");
        var code = command.Code is null ? entity.Code : Required(command.Code, 32, "Code");
        await context.EnsureUniqueEnterpriseCodeAsync(code, entity.Id, cancellationToken);
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        entity.Update(code, command.Name, command.Description);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
