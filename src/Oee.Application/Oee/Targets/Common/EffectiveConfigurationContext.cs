using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Oee.Targets.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác EffectiveConfiguration.</summary>
internal sealed class EffectiveConfigurationContext(
    IMasterDataStore masters,
    IOeeTargetStore targets,
    IApplicationUnitOfWork unitOfWork)
    : IUseCaseContext
{
    internal IMasterDataStore Masters => masters;
    internal IOeeTargetStore Targets => targets;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
}
