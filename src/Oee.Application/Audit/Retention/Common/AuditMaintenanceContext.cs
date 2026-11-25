using Oee.Application.Common;
using Oee.Application.Security;

namespace Oee.Application.Audit.Retention.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác AuditMaintenance.</summary>
internal sealed class AuditMaintenanceContext(
    IAuditRetentionStore store,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor currentActor,
    AuditRetentionOptions options,
    ICapabilityChecker? capabilityChecker = null)
    : IUseCaseContext
{
    internal IAuditRetentionStore Store => store;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal IClock Clock => clock;
    internal ICurrentActor CurrentActor => currentActor;
    internal AuditRetentionOptions Options => options;

    internal void EnsureAuthorized()
    {
        if (capabilityChecker is not null && !capabilityChecker.HasCapability(Capabilities.AuditRetentionManage))
        {
            throw new UnauthorizedAccessException("The audit.retention.manage capability is required.");
        }
    }

}
