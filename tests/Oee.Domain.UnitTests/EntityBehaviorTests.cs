using System.Collections;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Oee;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;
using Oee.Tests.Fixtures;

namespace Oee.Domain.UnitTests;

public sealed class EntityBehaviorTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly EffectiveOn = new(2026, 10, 19);

    [Fact]
    public void EveryPersistedEntityCanBeCreatedThroughARealFactory()
    {
        var enterprise = EnterpriseEntity.Create("E", "Enterprise");
        var site = SiteEntity.Create(enterprise.Id, "S", "Site", "Asia/Ho_Chi_Minh");
        var area = AreaEntity.Create(site.Id, "A", "Area");
        var center = WorkCenterEntity.Create(area.Id, "C", "Center");
        var unit = WorkUnitEntity.Create(center.Id, "M", "Machine");
        var shift = ShiftEntity.Create(site.Id, "S1", "Shift", new(6, 0), new(14, 0));
        var product = ProductEntity.Create("SKU", "Product", "PCS");
        var standard = WorkUnitStandardEntity.Create(unit.Id, product.Id, 10, "PCS", EffectiveOn);
        var reason = DowntimeReasonEntity.Create("BREAK", "Breakdown", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown);
        var plan = ProductionPlanEntity.Create(unit.Id, product.Id, "P", At, At.AddHours(8), 100, shift.Id);
        var sample = SampleProductionPlanEntity.Create("SAMPLE", "Sample");
        var sampleItem = SampleProductionPlanItemEntity.Create(sample.Id, unit.Id, product.Id, shift.Id, 100);
        var output = ProductionOutputEntity.Create(plan.Id, unit.Id, product.Id, At, At.AddMinutes(1), 10, 10, DataSourceEnum.Manual);
        var unplanned = ProductionReconciliationItemEntity.Create(unit.Id, "frame", At, 10, 0, "NO_ACTIVE_PLAN");
        var downtime = DowntimeEventEntity.Create(unit.Id, reason.Id, reason.Category, reason.Kind, At, DataSourceEnum.Manual);
        var target = OeeTargetEntity.Create(OeePeriodTypeEnum.Shift, "SHIFT", .85m, .9m, .9m, .99m, EffectiveOn);
        var calculation = Calculate();
        var snapshot = OeeSnapshotEntity.Create(unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S1", At, At.AddHours(8), calculation, 300, 3, At.AddHours(1), OeeCalculationEngine.CalculationVersion);
        var alert = OeeAlertEntity.Create(snapshot.Id, unit.Id, AlertMetricEnum.Oee, AlertSeverityEnum.Warning, .5m, .85m, At, target.Id);
        var connection = OpcUaConnectionEntity.Create("OPC", "Server", "opc.tcp://localhost:4840", "None", "None");
        var device = PlcDeviceMappingEntity.Create(connection.Id, unit.Id, "M", CounterModeEnum.Cumulative, FaultCodeFormatEnum.Auto);
        var tag = OpcUaTagMappingEntity.Create(device.Id, TagRoleEnum.Running, TagDirectionEnum.Read, "ns=2;s=Run", OpcUaDataTypeEnum.BooleanValue, true);
        var fault = PlcFaultCodeMappingEntity.Create("1", reason.Id, unit.Id);
        var checkpoint = CounterCheckpointEntity.Create(device.Id);
        var runtime = PlcDeviceRuntimeStateEntity.Create(device.Id);
        var integration = PlcIntegrationEventEntity.Create(At, IntegrationEventLevelEnum.Information, "Connectivity", "Connected", device.Id);
        var session = PlcSessionEventEntity.Create(connection.Id, At, ConnectivityStateEnum.Connected, "Connected");
        var audit = AuditLogEntity.Create(At, "Create", "Enterprise", enterprise.Id.ToString(), changes: "{}");
        var retention = AuditRetentionRunEntity.Create(At, At, At.AddMonths(-3), 3, 10, 10, "System");
        var outbox = OutboxMessageEntity.Create("frame", At, "PlcTelemetryFrame", "{}", At.AddHours(72));
        Entity[] entities = [enterprise, site, area, center, unit, shift, product, standard, reason, plan, sample, sampleItem,
            output, unplanned, downtime, target, snapshot, alert, connection, device, tag, fault, checkpoint, runtime, integration, session, audit, retention, outbox];
        var persistedTypes = typeof(DomainAssembly).Assembly.GetExportedTypes().Where(type => !type.IsAbstract && typeof(Entity).IsAssignableFrom(type)).OrderBy(type => type.FullName);
        Assert.Equal(persistedTypes, entities.Select(entity => entity.GetType()).OrderBy(type => type.FullName));
        Assert.All(entities, entity => Assert.NotEqual(Guid.Empty, entity.Id));
        Assert.Equal(entities.Length, entities.Select(entity => entity.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidMasterNameCannotPartiallyChangeTheEntity(string? name)
    {
        var entity = EnterpriseEntity.Create(" E ", " Original ", " description ");
        Assert.Equal("E", entity.Code); Assert.Equal("Original", entity.Name); Assert.Equal("description", entity.Description);
        Assert.Equal(RecordStatusEnum.Active, entity.Status);
        Assert.Throws<ArgumentException>(() => entity.Update("CHANGED", name!, "new"));
        Assert.Equal("E", entity.Code); Assert.Equal("Original", entity.Name); Assert.Equal("description", entity.Description);
    }

    [Fact]
    public void MastersRejectMissingParentsOversizedValuesAndUndefinedStatus()
    {
        Assert.Throws<ArgumentException>(() => AreaEntity.Create(Guid.Empty, "A", "Area"));
        Assert.Throws<ArgumentException>(() => ProductEntity.Create(new string('X', 33), "SKU", "PCS"));
        var enterprise = EnterpriseEntity.Create("E", "Enterprise");
        Assert.Throws<ArgumentOutOfRangeException>(() => enterprise.ChangeStatus((RecordStatusEnum)99));
        Assert.Equal(RecordStatusEnum.Active, enterprise.Status);
        Assert.Throws<ArgumentException>(() => ShiftEntity.Create(Guid.NewGuid(), "S", "Shift", new(6, 0), new(6, 0)));
        Assert.NotNull(ShiftEntity.Create(Guid.NewGuid(), "S3", "Night", new(22, 0), new(6, 0)));
    }

    [Fact]
    public void ProtectedReasonAndClassificationAreEnforcedInsideDomain()
    {
        var reason = DowntimeReasonEntity.Create("DR-UNKNOWN", "Unknown", DowntimeCategoryEnum.Unknown, DowntimeKindEnum.Unknown);
        Assert.Throws<InvalidOperationException>(() => reason.ChangeStatus(RecordStatusEnum.Inactive));
        Assert.Throws<InvalidOperationException>(() => reason.Update("Changed", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown, null));
        Assert.Equal("Unknown", reason.Name); Assert.Equal(DowntimeKindEnum.Unknown, reason.Kind);
        Assert.Throws<ArgumentException>(() => DowntimeReasonEntity.Create("BAD", "Bad", DowntimeCategoryEnum.QualityLoss, DowntimeKindEnum.Breakdown));
    }

    [Fact]
    public void PlansValidateBeforeMutationAndPreserveVersionHistory()
    {
        var plan = ProductionPlanEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "P", At, At.AddHours(8), 100);
        Assert.Throws<ArgumentException>(() => plan.Update(plan.WorkUnitId, plan.ProductId, "NEW", At, At, 50, null, null));
        Assert.Equal("P", plan.Code); Assert.Equal(100, plan.PlannedQuantity);
        Assert.Throws<ArgumentException>(() => plan.CreateCorrection(At, new string('X', 33)));
        Assert.Null(plan.UpdatedAt);
        var next = plan.CreateCorrection(At, "P2");
        Assert.Equal(plan.VersionSeriesId, next.VersionSeriesId); Assert.Equal(plan.Id, next.ReplacesPlanId); Assert.Equal(2, next.Version);
        plan.Delete(At);
        Assert.Throws<InvalidOperationException>(() => plan.Update(plan.WorkUnitId, plan.ProductId, "P3", At, At.AddHours(8), 50, null, null));
    }

    [Fact]
    public void ProductionFactsRejectInvalidQuantitiesSpeedAndTimeWindow()
    {
        var plan = Guid.NewGuid(); var unit = Guid.NewGuid(); var sku = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductionOutputEntity.Create(plan, unit, sku, At, At.AddMinutes(1), 10, 10, DataSourceEnum.Manual, defectQuantity: 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductionOutputEntity.Create(plan, unit, sku, At, At.AddMinutes(1), 0, 10, DataSourceEnum.Manual));
        Assert.Throws<ArgumentException>(() => ProductionOutputEntity.Create(plan, unit, sku, At, At, 10, 10, DataSourceEnum.Manual));
        Assert.Throws<ArgumentException>(() => WorkUnitStandardEntity.Create(unit, sku, 10, "PCS", EffectiveOn, EffectiveOn.AddDays(-1)));
        Assert.Throws<ArgumentException>(() => OeeTargetEntity.Create(OeePeriodTypeEnum.Day, "DAY", .8m, .9m, .9m, 1, EffectiveOn, unit, Guid.NewGuid()));
    }

    [Fact]
    public void SnapshotOwnsDerivedDurationsAndRatios()
    {
        var result = Calculate();
        var snapshot = OeeSnapshotEntity.Create(Guid.NewGuid(), OeePeriodTypeEnum.Shift, "SHIFT", At, At.AddHours(8), result, 300, 3, At.AddHours(1), "OEE-v1");
        Assert.Equal(.495m, snapshot.Oee); Assert.Equal(30, snapshot.PerformanceLossMinutes);
        Assert.Equal(.3m, snapshot.QualityLossMinutes); Assert.Equal(29.7m, snapshot.ValueAddedMinutes);
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.ApplyCalculation(At.AddHours(8), result with { Performance = 2 }, 300, 3, At.AddHours(2)));
        Assert.Equal(.5m, snapshot.Performance); Assert.Equal(At.AddHours(1), snapshot.CalculatedAt);
    }

    [Fact]
    public void InvalidAcknowledgementCannotChangeAlertState()
    {
        var alert = OeeAlertEntity.Create(Guid.NewGuid(), Guid.NewGuid(), AlertMetricEnum.Oee, AlertSeverityEnum.Warning, .5m, .85m, At);
        Assert.Throws<ArgumentException>(() => alert.Acknowledge(At, " ", null));
        Assert.Equal(AlertStateEnum.Open, alert.State); Assert.Null(alert.AcknowledgedAt);
        Assert.Throws<ArgumentException>(() => alert.Acknowledge(At, "operator", new string('X', 501)));
        Assert.Equal(AlertStateEnum.Open, alert.State);
        alert.Acknowledge(At, "operator", "seen"); alert.Resolve(At.AddMinutes(1), "operator", null);
        Assert.Throws<InvalidOperationException>(() => alert.Refresh(.6m, .85m, AlertSeverityEnum.Warning));
    }

    [Fact]
    public void ConnectionUpdatesKeepOrClearCredentialsAtomically()
    {
        var connection = OpcUaConnectionEntity.Create("C", "Server", "opc.tcp://localhost:4840", "None", "None", "UserName", "user", "env:OPC_SECRET");
        connection.Update("C", "Updated", connection.EndpointUrl, "None", "None", "UserName", "user", null, null, true);
        Assert.Equal("env:OPC_SECRET", connection.CredentialSecretReference);
        Assert.Throws<ArgumentException>(() => connection.Update("C", "Bad", connection.EndpointUrl, "None", "None", "UserName", "user", null, null, true, clearCredentialSecret: true));
        Assert.Equal("Updated", connection.Name); Assert.Equal("env:OPC_SECRET", connection.CredentialSecretReference);
        connection.Update("C", "Anonymous", connection.EndpointUrl, "None", "None", "Anonymous", "user", null, null, true);
        Assert.Null(connection.UserName); Assert.Null(connection.CredentialSecretReference);
    }

    [Fact]
    public void InvalidTagOrDeviceDefinitionIsRejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => OpcUaTagMappingEntity.Create(id, TagRoleEnum.Running, TagDirectionEnum.Write, "ns=2;s=Run", OpcUaDataTypeEnum.BooleanValue, true));
        Assert.Throws<ArgumentException>(() => OpcUaTagMappingEntity.Create(id, TagRoleEnum.Running, TagDirectionEnum.Read, "ns=2;s=Run", OpcUaDataTypeEnum.Text, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlcDeviceMappingEntity.Create(id, Guid.NewGuid(), "M", CounterModeEnum.Cumulative, FaultCodeFormatEnum.Auto, debounceSamples: 21));
        var device = PlcDeviceMappingEntity.Create(id, Guid.NewGuid(), "M", CounterModeEnum.Cumulative, FaultCodeFormatEnum.Hex);
        Assert.Equal("10", device.NormalizeFault("0x0A")); Assert.Null(device.NormalizeFault("0x00"));
    }

    [Fact]
    public void CounterDiscontinuityDoesNotCountTheRestoredAbsoluteValue()
    {
        var checkpoint = CounterCheckpointEntity.Create(Guid.NewGuid());
        var first = CounterDeltaCalculator.Calculate(checkpoint.State, new(2833, 0, At, "first"), CounterModeEnum.Cumulative);
        checkpoint.Apply(first, "first", At);
        var dropped = CounterDeltaCalculator.Calculate(checkpoint.State, new(0, 0, At.AddSeconds(1), "zero"), CounterModeEnum.Cumulative);
        Assert.False(dropped.Accepted);
        Assert.Throws<InvalidOperationException>(() => checkpoint.Apply(dropped, "zero", At.AddSeconds(1)));
        checkpoint.MarkDiscontinuity();
        var restored = CounterDeltaCalculator.Calculate(checkpoint.State, new(2861, 0, At.AddSeconds(2), "restored"), CounterModeEnum.Cumulative, isReconnect: checkpoint.IsDiscontinuityPending);
        Assert.Equal(28, restored.TotalDelta);
        checkpoint.Apply(restored, "restored", At.AddSeconds(2));
        Assert.Equal(2861, checkpoint.LastTotalCount); Assert.False(checkpoint.IsDiscontinuityPending);
        checkpoint.Reset();
        Assert.Equal(1, checkpoint.CounterEpoch); Assert.Null(checkpoint.LastReadAt); Assert.Null(checkpoint.LastCounterChangedAt);
    }

    [Fact]
    public void RuntimeDebouncesTransitionsAndReconnectOnlyRestoresState()
    {
        var runtime = PlcDeviceRuntimeStateEntity.Create(Guid.NewGuid(), ConnectivityStateEnum.Connected, OperationalStateEnum.Running, At);
        Assert.Null(runtime.Observe(OperationalStateEnum.Stopped, At.AddSeconds(1), At.AddSeconds(1), false, null, 2));
        Assert.Equal(OperationalStateEnum.Running, runtime.OperationalState);
        Assert.Equal(OperationalStateEnum.Running, runtime.Observe(OperationalStateEnum.Stopped, At.AddSeconds(2), At.AddSeconds(2), false, null, 2));
        Assert.Equal(At.AddSeconds(2), runtime.LastTransitionAt);
        Assert.Null(runtime.Observe(OperationalStateEnum.Running, At.AddSeconds(3), At.AddSeconds(3), true, null, 2));
        Assert.Equal(OperationalStateEnum.Running, runtime.OperationalState); Assert.Equal(0, runtime.PendingSampleCount);
        Assert.Equal(At.AddSeconds(2), runtime.LastTransitionAt);
        runtime.MarkConnectivity(ConnectivityStateEnum.Disconnected);
        Assert.Equal(OperationalStateEnum.Running, runtime.OperationalState);
    }

    [Fact]
    public void CollectionsAndConcurrencyTokensDoNotExposeMutableState()
    {
        var sample = SampleProductionPlanEntity.Create("S", "Sample");
        Assert.True(((IList)sample.Items).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((IList)sample.Items).Add(null));
        var checkpoint = CounterCheckpointEntity.Create(Guid.NewGuid());
        byte[] token = [1, 2];
        DomainTestData.Set(checkpoint, nameof(CounterCheckpointEntity.RowVersion), token);
        token[0] = 9;
        var exposed = checkpoint.RowVersion; exposed[0] = 8;
        Assert.Equal(1, checkpoint.RowVersion[0]);
    }

    [Fact]
    public void OutboxAndAuditHistoryUseExplicitOperations()
    {
        var message = OutboxMessageEntity.Create("K", At, "Frame", "{}", At.AddHours(72));
        message.RecordFailure(new string('X', 5000));
        Assert.Equal(1, message.AttemptCount); Assert.Equal(4000, message.LastError!.Length);
        message.MarkProcessed(At.AddMinutes(1)); Assert.Null(message.LastError); Assert.Equal(At.AddMinutes(1), message.ProcessedAt);
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => AuditLogEntity.Create(At, "Create", "Entity", "1", changes: "invalid"));
    }

    private static OeeCalculationResultValue Calculate() => OeeCalculationEngine.Calculate(new(At, At.AddHours(1), 0, 300, 3, .1m, []));
}
