using System.Globalization;
using Oee.Application.Common;
using Oee.Application.Security;
using Oee.Domain.Entities.Oee;

namespace Oee.Application.Oee.Alerts.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác AlertModule.</summary>
internal sealed class AlertModuleContext(
    IUseCaseExecutor requests,
    IAlertReadStore reads,
    IOeeAlertStore alerts,
    IOeeTargetStore targets,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor currentActor)
    : IUseCaseContext
{
    internal IUseCaseExecutor Requests => requests;
    internal IAlertReadStore Reads => reads;
    internal IOeeAlertStore Alerts => alerts;
    internal IOeeTargetStore Targets => targets;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal IClock Clock => clock;
    internal ICurrentActor CurrentActor => currentActor;

    internal async Task<OeeAlertEntity> FindAlertAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await alerts.FindAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Alert not found.");

    internal static void EnsureConcurrency(OeeAlertEntity alert, string token)
    {
        if (!DateTimeOffset.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expected)
            || expected.ToUniversalTime() != (alert.UpdatedAt ?? alert.CreatedAt).ToUniversalTime())
            throw new ConcurrencyConflictException("The alert changed after it was loaded.");
    }

    internal void Touch(OeeAlertEntity alert)
    {
        alert.RecordUpdate(clock.UtcNow, currentActor.UserName);
    }

    internal void SetExpectedRowVersion(OeeTargetEntity target, string rowVersion)
    {
        byte[] expected;

        try
        {
            expected = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException exception)
        {
            throw new ConcurrencyConflictException("Invalid target concurrency token.", exception);
        }

        unitOfWork.ExpectRowVersion(target, expected);
    }
}
