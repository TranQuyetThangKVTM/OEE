using Oee.Application.Common;
using Oee.Application.Operations;
using Oee.Application.Security;

namespace Oee.Application.Organizations.Hierarchy.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác HierarchyMutation.</summary>
internal sealed class HierarchyMutationContext(
    IUseCaseExecutor requests,
    IMasterDataStore store,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    ICurrentActor? currentActor = null,
    ICapabilityChecker? capabilityChecker = null)
    : IUseCaseContext
{
    internal IUseCaseExecutor Requests => requests;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal MasterDataCommandContext context { get; } = new(store, unitOfWork, clock, currentActor, capabilityChecker);
}
