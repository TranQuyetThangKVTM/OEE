using Oee.Application.Common;
using Oee.Application.Oee.Alerts.Common;

namespace Oee.Application.Oee.Alerts.Delete;

/// <summary>Điều phối thao tác DeleteOeeTarget trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteOeeTargetHandler(AlertModuleContext context)
    : ICommandHandler<DeleteOeeTargetCommand>
{
    public async Task HandleAsync(DeleteOeeTargetCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        var target = await context.Targets.FindAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("OEE target not found.");

        if (await context.Alerts.HasTargetHistoryAsync(id, cancellationToken))
            throw new InvalidOperationException("A target referenced by alert history cannot be deleted; end its effective period instead.");
        context.SetExpectedRowVersion(target, rowVersion);
        target.SoftDelete(context.Clock.UtcNow, context.CurrentActor.UserName);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
