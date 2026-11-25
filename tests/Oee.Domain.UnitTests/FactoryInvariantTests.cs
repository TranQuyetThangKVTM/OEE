using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;

namespace Oee.Domain.UnitTests;

public sealed class FactoryInvariantTests
{
    [Theory]
    [InlineData(RecordStatusEnum.Active)]
    [InlineData(RecordStatusEnum.Inactive)]
    public void MastersUseTypedStatusForCreationAndValidatedChanges(RecordStatusEnum status)
    {
        var parentId = Guid.NewGuid();
        IStatusEntity[] masters =
        [
            EnterpriseEntity.Create("E", "Enterprise", status: status),
            SiteEntity.Create(parentId, "S", "Site", "Asia/Ho_Chi_Minh", status: status),
            AreaEntity.Create(parentId, "A", "Area", status: status),
            WorkCenterEntity.Create(parentId, "C", "Center", status: status),
            WorkUnitEntity.Create(parentId, "M", "Machine", status: status),
            ShiftEntity.Create(parentId, "S1", "Shift", new(6, 0), new(14, 0), status),
            ProductEntity.Create("SKU", "Product", "PCS", status: status),
            DowntimeReasonEntity.Create("BREAK", "Breakdown", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown, status: status)
        ];

        Assert.All(masters, master => Assert.Equal(status, master.Status));
        var next = status == RecordStatusEnum.Active ? RecordStatusEnum.Inactive : RecordStatusEnum.Active;
        foreach (var master in masters)
        {
            master.ChangeStatus(next);
            Assert.Equal(next, master.Status);
            Assert.Throws<ArgumentOutOfRangeException>(() => master.ChangeStatus((RecordStatusEnum)99));
            Assert.Equal(next, master.Status);
        }
    }

    [Fact]
    public void MastersRejectUndefinedStatusDuringCreation()
    {
        var parentId = Guid.NewGuid();
        const RecordStatusEnum invalidStatus = (RecordStatusEnum)99;
        Action[] create =
        [
            () => EnterpriseEntity.Create("E", "Enterprise", status: invalidStatus),
            () => SiteEntity.Create(parentId, "S", "Site", "Asia/Ho_Chi_Minh", status: invalidStatus),
            () => AreaEntity.Create(parentId, "A", "Area", status: invalidStatus),
            () => WorkCenterEntity.Create(parentId, "C", "Center", status: invalidStatus),
            () => WorkUnitEntity.Create(parentId, "M", "Machine", status: invalidStatus),
            () => ShiftEntity.Create(parentId, "S1", "Shift", new(6, 0), new(14, 0), invalidStatus),
            () => ProductEntity.Create("SKU", "Product", "PCS", status: invalidStatus),
            () => DowntimeReasonEntity.Create("BREAK", "Breakdown", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown, status: invalidStatus)
        ];

        Assert.All(create, operation => Assert.Throws<ArgumentOutOfRangeException>(operation));
    }

    [Fact]
    public void ProtectedUnknownReasonCannotBeCreatedInactiveOrWithADifferentClassification()
    {
        Assert.Throws<InvalidOperationException>(() => DowntimeReasonEntity.Create(" DR-UNKNOWN ", "Unknown",
            DowntimeCategoryEnum.Unknown, DowntimeKindEnum.Unknown, status: RecordStatusEnum.Inactive));
        Assert.Throws<InvalidOperationException>(() => DowntimeReasonEntity.Create(" DR-UNKNOWN ", "Unknown",
            DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown));

        var reason = DowntimeReasonEntity.Create(" DR-UNKNOWN ", " Unknown ", DowntimeCategoryEnum.Unknown, DowntimeKindEnum.Unknown);
        Assert.Equal("DR-UNKNOWN", reason.Code);
        Assert.Equal("Unknown", reason.Name);
        Assert.Equal(RecordStatusEnum.Active, reason.Status);
    }

    [Fact]
    public void PlansStartTheirVersionSeriesWithTheirOwnIdAndKeepSampleReferences()
    {
        var start = new DateTimeOffset(2026, 10, 19, 22, 0, 0, TimeSpan.FromHours(7));
        var unitId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var suppliedId = Guid.NewGuid();
        var generated = ProductionPlanEntity.Create(unitId, productId, "P1", start, start.AddHours(8), 100);
        var supplied = ProductionPlanEntity.Create(unitId, productId, " P2 ", start, start.AddHours(8), 100, shiftId,
            " notes ", sampleId, itemId, suppliedId);

        Assert.NotEqual(Guid.Empty, generated.Id);
        Assert.Equal(generated.Id, generated.VersionSeriesId);
        Assert.Equal(suppliedId, supplied.Id);
        Assert.Equal(suppliedId, supplied.VersionSeriesId);
        Assert.Equal(1, supplied.Version);
        Assert.Equal(sampleId, supplied.SourceSampleProductionPlanId);
        Assert.Equal(itemId, supplied.SourceSampleProductionPlanItemId);
        Assert.Equal(shiftId, supplied.ShiftId);
        Assert.Equal("P2", supplied.Code);
        Assert.Equal("notes", supplied.Notes);
        Assert.Equal(TimeSpan.Zero, supplied.PlannedStart.Offset);
        Assert.Equal(start, supplied.PlannedStart);
        Assert.Equal(start.AddHours(8), supplied.PlannedEnd);
        Assert.Throws<ArgumentException>(() => ProductionPlanEntity.Create(unitId, productId, "P3", start, start.AddHours(8), 100, id: Guid.Empty));
        Assert.Throws<ArgumentException>(() => ProductionPlanEntity.Create(unitId, productId, "P3", start, start.AddHours(8), 100,
            sourceSampleProductionPlanItemId: Guid.Empty));
    }

    [Theory]
    [InlineData("http://localhost:4840", "None", "Anonymous", null, null)]
    [InlineData("opc.tcp://localhost:4840", "Unknown", "Anonymous", null, null)]
    [InlineData("opc.tcp://localhost:4840", "None", "Unknown", null, null)]
    [InlineData("opc.tcp://localhost:4840", "None", "UserName", " ", "env:OPC_SECRET")]
    [InlineData("opc.tcp://localhost:4840", "None", "UserName", "operator", null)]
    public void ConnectionsUseTheSameRulesForCreationAndUpdate(string endpoint, string mode, string auth, string? user, string? secret)
    {
        Assert.ThrowsAny<ArgumentException>(() => OpcUaConnectionEntity.Create("C", "New", endpoint, mode, "None", auth, user, secret));

        var connection = OpcUaConnectionEntity.Create("C", "Original", "opc.tcp://localhost:4840", "None", "None");
        Assert.ThrowsAny<ArgumentException>(() => connection.Update("CHANGED", "Changed", endpoint, mode, "None", auth, user,
            secret, null, true, keepExistingSecret: false));
        Assert.Equal("C", connection.Code);
        Assert.Equal("Original", connection.Name);
        Assert.Equal("opc.tcp://localhost:4840", connection.EndpointUrl);
        Assert.Equal("Anonymous", connection.AuthenticationType);
    }

    [Fact]
    public void AnonymousConnectionsDiscardCredentialsAndInactiveUsernameConnectionsCanWaitForASecret()
    {
        var anonymous = OpcUaConnectionEntity.Create("C", "Server", "opc.tcp://localhost:4840", "None", "None",
            "Anonymous", "operator", "env:OPC_SECRET");
        Assert.Null(anonymous.UserName);
        Assert.Null(anonymous.CredentialSecretReference);

        var inactive = OpcUaConnectionEntity.Create("C", "Server", "opc.tcp://localhost:4840", "None", "None",
            "UserName", " operator ", isActive: false);
        Assert.Equal("operator", inactive.UserName);
        Assert.Null(inactive.CredentialSecretReference);
        Assert.False(inactive.IsActive);
    }

    [Theory]
    [InlineData(TagRoleEnum.Running, OpcUaDataTypeEnum.BooleanValue)]
    [InlineData(TagRoleEnum.TotalCounter, OpcUaDataTypeEnum.SinglePrecision)]
    public void RequiredCounterInputsCannotBeMadeOptional(TagRoleEnum role, OpcUaDataTypeEnum dataType)
    {
        var tag = OpcUaTagMappingEntity.Create(Guid.NewGuid(), role, TagDirectionEnum.Read, "ns=2;s=Input", dataType, false);
        Assert.True(tag.IsRequired);
        tag.Update("ns=2;s=Updated", dataType, false);
        Assert.True(tag.IsRequired);
    }
}
