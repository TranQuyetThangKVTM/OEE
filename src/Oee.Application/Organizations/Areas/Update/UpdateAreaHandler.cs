using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.Areas.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Areas.Update;

/// <summary>Điều phối thao tác UpdateArea trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateAreaHandler(AreaContext context)
    : IRequestHandler<UpdateAreaCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateAreaCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindAreaAsync(command.Id, cancellationToken) ?? throw Missing("Area");
        var code = command.Code is null ? entity.Code : Required(command.Code, 32, "Code");
        await context.EnsureUniqueAreaCodeAsync(entity.SiteId, code, entity.Id, cancellationToken);
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        entity.Update(code, command.Name, command.Description);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
