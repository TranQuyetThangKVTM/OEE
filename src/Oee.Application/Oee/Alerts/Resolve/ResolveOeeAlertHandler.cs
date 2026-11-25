using Oee.Application.Common;
using Oee.Application.Oee.Alerts.Common;

namespace Oee.Application.Oee.Alerts.Resolve;

/// <summary>Điều phối thao tác ResolveOeeAlert trong cùng scope lưu dữ liệu.</summary>
internal sealed class ResolveOeeAlertHandler(AlertModuleContext context)
    : ICommandHandler<ResolveOeeAlertCommand>
{
    public async Task HandleAsync(ResolveOeeAlertCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var request = command.Request;
        var alert = await context.FindAlertAsync(id, cancellationToken);
        AlertModuleContext.EnsureConcurrency(alert, request.ConcurrencyToken);
        alert.Resolve(context.Clock.UtcNow, context.CurrentActor.UserName, request.Note);
        context.Touch(alert);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
