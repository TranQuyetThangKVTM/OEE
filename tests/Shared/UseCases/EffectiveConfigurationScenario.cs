using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Oee;
using Oee.Application.Operations;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class EffectiveConfigurationScenario : IEffectiveConfigurationScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public EffectiveConfigurationScenario(IMasterDataStore masters, IOeeTargetStore targets, IApplicationUnitOfWork unitOfWork)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IMasterDataStore)] = masters, [typeof(IOeeTargetStore)] = targets, [typeof(IApplicationUnitOfWork)] = unitOfWork });
    }

    public Task<Guid> AddStandardVersionAsync(EffectiveStandardRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Targets.AddStandardVersion.AddStandardVersionCommand(request), ct);
    public Task<Guid> AddTargetVersionAsync(EffectiveTargetRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Targets.AddTargetVersion.AddTargetVersionCommand(request), ct);
}
