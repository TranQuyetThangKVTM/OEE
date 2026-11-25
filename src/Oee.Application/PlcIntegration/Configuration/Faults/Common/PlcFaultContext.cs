using Oee.Application.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác PlcFault.</summary>
internal sealed class PlcFaultContext(PlcConfigurationContext context) : IUseCaseContext
{
    internal PlcConfigurationContext Context => context;
    internal IPlcConfigurationStore store => context.Store;

    internal async Task ValidateFaultReferencesAndUniquenessAsync(
        PlcFaultValues values,
        Guid? except,
        CancellationToken cancellationToken)
    {
        var workUnitId = values.WorkUnitId;
        var reasonId = values.ReasonId;
        var faultCode = values.FaultCode;
        var active = values.Active;

        if (workUnitId.HasValue
            && !await context.Masters.AnyWorkUnitsAsync(x => x.Id == workUnitId, cancellationToken))
            throw Missing("WorkUnit");

        if (!await context.Masters.AnyDowntimeReasonsAsync(x => x.Id == reasonId, cancellationToken))
            throw Missing("Downtime reason");

        if (active
            && await store.AnyFaultsAsync(
            x => x.Id != except && x.IsActive && x.WorkUnitId == workUnitId && x.FaultCode == faultCode,
            cancellationToken))
            throw new InvalidOperationException("An active fault mapping already exists in this scope.");
    }
}
