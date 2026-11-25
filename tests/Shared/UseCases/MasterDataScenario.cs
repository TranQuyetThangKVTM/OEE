using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Security;
using Oee.Application.Catalog.Products.Create;
using Oee.Application.Catalog.Products.SetProductStatus;
using Oee.Application.Catalog.Products.Update;
using Oee.Application.Catalog.Standards.Create;
using Oee.Application.Catalog.Standards.EndStandardEffectiveness;
using Oee.Application.Downtime.Reasons.Create;
using Oee.Application.Downtime.Reasons.SetDowntimeReasonStatus;
using Oee.Application.Downtime.Reasons.Update;
using Oee.Application.Organizations.Areas.Create;
using Oee.Application.Organizations.Areas.SetAreaStatus;
using Oee.Application.Organizations.Areas.Update;
using Oee.Application.Organizations.Enterprises.Create;
using Oee.Application.Organizations.Enterprises.SetEnterpriseStatus;
using Oee.Application.Organizations.Enterprises.Update;
using Oee.Application.Organizations.Shifts.Create;
using Oee.Application.Organizations.Shifts.SetShiftStatus;
using Oee.Application.Organizations.Shifts.Update;
using Oee.Application.Organizations.Sites.Create;
using Oee.Application.Organizations.Sites.SetSiteStatus;
using Oee.Application.Organizations.Sites.Update;
using Oee.Application.Organizations.WorkCenters.Create;
using Oee.Application.Organizations.WorkCenters.SetWorkCenterStatus;
using Oee.Application.Organizations.WorkCenters.Update;
using Oee.Application.Organizations.WorkUnits.Create;
using Oee.Application.Organizations.WorkUnits.SetWorkUnitStatus;
using Oee.Application.Organizations.WorkUnits.Update;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class MasterDataScenario : IMasterDataScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public MasterDataScenario(IMasterDataStore store, IApplicationUnitOfWork unitOfWork, IClock clock, ICurrentActor? currentActor = null, ICapabilityChecker? capabilityChecker = null)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IMasterDataStore)] = store, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock, [typeof(ICurrentActor)] = currentActor, [typeof(ICapabilityChecker)] = capabilityChecker });
    }

    public Task<MasterMutationResult> CreateProductAsync(CreateProductCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateProductAsync(UpdateProductCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetProductStatusAsync(SetProductStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteProductAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Catalog.Products.Delete.DeleteProductCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateStandardAsync(CreateStandardCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> EndStandardEffectivenessAsync(EndStandardEffectivenessCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteStandardAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Catalog.Standards.Delete.DeleteStandardCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateShiftAsync(CreateShiftCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateShiftAsync(UpdateShiftCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetShiftStatusAsync(SetShiftStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteShiftAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Organizations.Shifts.Delete.DeleteShiftCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateDowntimeReasonAsync(CreateDowntimeReasonCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateDowntimeReasonAsync(UpdateDowntimeReasonCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetDowntimeReasonStatusAsync(SetDowntimeReasonStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteDowntimeReasonAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Downtime.Reasons.Delete.DeleteDowntimeReasonCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateWorkUnitAsync(CreateWorkUnitCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateWorkUnitAsync(UpdateWorkUnitCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetWorkUnitStatusAsync(SetWorkUnitStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteWorkUnitAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Organizations.WorkUnits.Delete.DeleteWorkUnitCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateWorkCenterAsync(CreateWorkCenterCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateWorkCenterAsync(UpdateWorkCenterCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetWorkCenterStatusAsync(SetWorkCenterStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteWorkCenterAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Organizations.WorkCenters.Delete.DeleteWorkCenterCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateAreaAsync(CreateAreaCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateAreaAsync(UpdateAreaCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetAreaStatusAsync(SetAreaStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteAreaAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Organizations.Areas.Delete.DeleteAreaCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateEnterpriseAsync(CreateEnterpriseCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateEnterpriseAsync(UpdateEnterpriseCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetEnterpriseStatusAsync(SetEnterpriseStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteEnterpriseAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Organizations.Enterprises.Delete.DeleteEnterpriseCommand(id, rowVersion), ct);
    public Task<MasterMutationResult> CreateSiteAsync(CreateSiteCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> UpdateSiteAsync(UpdateSiteCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task<MasterMutationResult> SetSiteStatusAsync(SetSiteStatusCommand request, CancellationToken ct) => this.requests.ExecuteAsync(request, ct);
    public Task DeleteSiteAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Organizations.Sites.Delete.DeleteSiteCommand(id, rowVersion), ct);
    public Task DeleteHierarchyAsync(IReadOnlyCollection<EquipmentHierarchyDeleteItem> items, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Organizations.Hierarchy.Delete.DeleteHierarchyCommand(items), ct);
}
