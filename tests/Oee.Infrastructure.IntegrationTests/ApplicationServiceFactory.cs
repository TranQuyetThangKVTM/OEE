using Oee.Application.Common;
using Oee.Application.PlcIntegration;
using Oee.Application.Security;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.PlcIntegration;

namespace Oee.Infrastructure.IntegrationTests;

internal static class ApplicationServiceFactory
{
    public static PlcConfigurationScenario PlcConfiguration(OeeDbContext db, IClock clock, IOpcUaClientAdapter adapter)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new EfPlcConfigurationStore(unit), new EfMasterDataStore(unit), new EfPlcConfigurationReadStore(unit), unit, clock, adapter);
    }
    public static OeeSnapshotScenario Snapshots(OeeDbContext db, IClock clock)
    {
        var unit = new EfApplicationUnitOfWork(db);
        var store = new EfOeeStore(unit);
        return new(store, store, unit, clock);
    }
    public static EffectiveConfigurationScenario EffectiveConfiguration(OeeDbContext db)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new EfMasterDataStore(unit), new EfOeeStore(unit), unit);
    }
    public static PlcTelemetryScenario Telemetry(OeeDbContext db, IClock clock)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new EfPlcTelemetryStore(unit), new EfProductionOutputStore(unit), new EfDowntimeStore(unit), unit, clock);
    }
    public static AlertModuleScenario Alerts(OeeDbContext db, IOeeSnapshotScenario snapshots, IEffectiveConfigurationScenario effectiveConfiguration, IClock clock, ICurrentActor actor)
    {
        var unit = new EfApplicationUnitOfWork(db);
        var store = new EfOeeStore(unit);
        return new(new EfAlertReadStore(unit, clock), store, store, unit, snapshots, effectiveConfiguration, clock, actor);
    }
    public static MasterDataScenario MasterData(OeeDbContext db, IClock clock, ICurrentActor? actor = null, ICapabilityChecker? capabilities = null)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new EfMasterDataStore(unit), unit, clock, actor, capabilities);
    }
    public static ProductionPlanScenario Plans(OeeDbContext db, IClock clock, ICurrentActor? actor = null, ICapabilityChecker? capabilities = null)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new EfProductionPlanStore(unit), unit, clock, actor, capabilities);
    }
    public static SampleProductionPlanScenario Samples(OeeDbContext db, IClock clock, ICurrentActor? actor = null)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new EfSampleProductionPlanStore(unit), unit, clock, actor);
    }
    public static DowntimeScenario Downtime(OeeDbContext db, IClock clock, ICapabilityChecker? capabilities = null)
    {
        var unit = new EfApplicationUnitOfWork(db);
        return new(new EfDowntimeStore(unit), unit, clock, capabilities);
    }
}
