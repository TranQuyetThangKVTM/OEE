using Oee.Application.Common;
using Oee.Application.Oee.Alerts.Common;

namespace Oee.Application.Oee.Alerts.EndTarget;

/// <summary>Điều phối thao tác EndOeeTarget trong cùng scope lưu dữ liệu.</summary>
internal sealed class EndOeeTargetHandler(AlertModuleContext context) : ICommandHandler<EndOeeTargetCommand>
{
    public async Task HandleAsync(EndOeeTargetCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var request = command.Request;
        var target = await context.Targets.FindAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("OEE target not found.");

        if (request.EffectiveTo < target.EffectiveFrom)
            throw new ArgumentException("End date cannot precede the effective start.", nameof(command));
        context.SetExpectedRowVersion(target, request.RowVersion);
        target.EndEffectiveness(request.EffectiveTo);
        target.RecordUpdate(context.Clock.UtcNow, context.CurrentActor.UserName);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
