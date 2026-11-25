using Oee.Application.Common;
using Oee.Application.Organizations.Enterprises.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Enterprises.Delete;

/// <summary>Điều phối thao tác DeleteEnterprise trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteEnterpriseHandler(EnterpriseContext context)
    : ICommandHandler<DeleteEnterpriseCommand>
{
    public async Task HandleAsync(DeleteEnterpriseCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindEnterpriseAsync(id, cancellationToken) ?? throw Missing("Enterprise");

        if (await context.store.AnySitesAsync(x => x.EnterpriseId == id, cancellationToken))
            throw Referenced("Enterprise");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
