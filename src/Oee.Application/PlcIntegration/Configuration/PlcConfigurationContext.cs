using Oee.Application.Common;
using Oee.Application.Operations;
using Oee.Application.Security;
using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration;

/// <summary>Dùng chung persistence port, unit of work, clock và adapter trong các nhóm cấu hình PLC.</summary>
internal sealed class PlcConfigurationContext(
    IPlcConfigurationStore store,
    IMasterDataStore masters,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    IOpcUaClientAdapter opcUa,
    ICapabilityChecker? capabilityChecker = null)
    : IUseCaseContext
{
    internal IPlcConfigurationStore Store => store;
    internal IMasterDataStore Masters => masters;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal IClock Clock => clock;
    internal IOpcUaClientAdapter OpcUa => opcUa;

    internal void EnsureAuthorized()
    {
        if (capabilityChecker is not null && !capabilityChecker.HasCapability(Capabilities.PlcManage))
            throw new UnauthorizedAccessException("The plc.manage capability is required.");
    }

    internal void ApplyConcurrency(SoftDeletableAuditedEntity entity, string value)
    {
        // Gửi RowVersion client qua port để persistence từ chối ghi đè dữ liệu đã thay đổi.
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new ArgumentException("RowVersion is invalid.", nameof(value));
        }
        unitOfWork.ExpectRowVersion(entity, expected);
    }

    internal void Touch(AuditedEntity entity) => entity.RecordUpdate(clock.UtcNow);

}
