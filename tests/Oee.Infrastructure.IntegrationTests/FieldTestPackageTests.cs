extern alias migrator;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;
using MasterDataSnapshot = migrator::Oee.DatabaseMigrator.MasterDataSnapshot;
using MasterDataTransfer = migrator::Oee.DatabaseMigrator.MasterDataTransfer;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class FieldTestPackageTests
{
    [Fact]
    public void ClientSettingsChangeEndpointIdentityTimeoutsAndCertificateStoresWithoutNetwork()
    {
        var client = new OpcUaClientOptions
        {
            Enabled = true, EndpointUrl = "opc.tcp://127.0.0.1:59101",
            ApplicationName = "MES.Masan.OEE.Client", ApplicationUri = "urn:masan:mes:oee:client",
            ProductUri = "urn:masan:mes:oee", PublishingIntervalMs = 500,
            OperationTimeoutMs = 12000, SessionTimeoutMs = 65000, AutoAcceptUntrustedCertificates = true,
            ApplicationCertificateSubject = "CN=MES.Masan.OEE.Client,O=Masan Consumer,C=VN",
        };
        Assert.True(client.IsValid());
        var original = new OpcUaEndpointOptions(Guid.NewGuid(), "opc.tcp://old:4840", "None", "None", "Anonymous", null, null, null);
        var applied = client.ApplyTo(original);
        Assert.Equal(original.ConnectionId, applied.ConnectionId);
        Assert.Equal(client.EndpointUrl, applied.EndpointUrl);
        Assert.Equal("Anonymous", applied.AuthenticationType);
        var adapter = new OpcUaSdkAdapter(new EnvironmentCredentialResolver(), Options.Create(client));
        var configuration = adapter.CreateConfiguration(applied);
        Assert.Equal(client.ApplicationName, configuration.ApplicationName);
        Assert.Equal(client.ApplicationUri, configuration.ApplicationUri);
        Assert.Equal(client.ProductUri, configuration.ProductUri);
        Assert.Equal(client.OperationTimeoutMs, configuration.TransportQuotas.OperationTimeout);
        Assert.Equal(client.SessionTimeoutMs, configuration.ClientConfiguration.DefaultSessionTimeout);
        Assert.True(configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates);
        Assert.EndsWith(Path.Combine("pki", "own"), configuration.SecurityConfiguration.ApplicationCertificate.StorePath, StringComparison.Ordinal);
        Assert.Equal(original, new OpcUaClientOptions().ApplyTo(original));
        Assert.False(new OpcUaClientOptions { Enabled = true, EndpointUrl = "https://invalid" }.IsValid());
        Assert.False(new OpcUaClientOptions { Enabled = true, EndpointUrl = client.EndpointUrl, Auth = new() { Mode = "Certificate" } }.IsValid());
        Assert.False(new OpcUaClientOptions { Enabled = true, EndpointUrl = client.EndpointUrl, UseSecurity = true }.IsValid());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task MasterDataExportImportPreservesTreeAndMappingsWithoutHistoryAndRejectsExistingData()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var sourceConnection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_PackageTests_Source_{Guid.NewGuid():N}" };
        var targetConnection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_PackageTests_Target_{Guid.NewGuid():N}" };
        var sourceOptions = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(sourceConnection.ConnectionString).Options;
        var targetOptions = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(targetConnection.ConnectionString).Options;
        await using var origin = new OeeDbContext(sourceOptions);
        await using var target = new OeeDbContext(targetOptions);
        var path = Path.Combine(Path.GetTempPath(), $"oee-master-test-{Guid.NewGuid():N}.json");
        try
        {
            await origin.Database.MigrateAsync();
            await target.Database.MigrateAsync();
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "MCH"), ("Name", "Masan"), ("Description", "Mô tả"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "MHG"), ("Name", "Hậu Giang"), ("TimeZoneId", "SE Asia Standard Time"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "CSD"), ("Name", "CSD"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "P02"), ("Name", "Packing"));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "M03"), ("Name", "Filler"));
            var connection = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "SERVER"), ("Name", "Server"), ("EndpointUrl", "opc.tcp://localhost:4840"), ("SecurityMode", "None"), ("SecurityPolicy", "None"), ("IsActive", true));
            var mapping = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", connection), ("WorkUnit", unit), ("ExternalDeviceId", "3"), ("IsActive", true));
            origin.AddRange(mapping,
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", mapping), ("TagRole", TagRoleEnum.Running), ("Direction", TagDirectionEnum.Read), ("NodeId", "ns=8;s=Run"), ("DataType", OpcUaDataTypeEnum.BooleanValue), ("IsRequired", true)),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", mapping), ("TagRole", TagRoleEnum.TotalCounter), ("Direction", TagDirectionEnum.Read), ("NodeId", "ns=8;s=Total"), ("DataType", OpcUaDataTypeEnum.Unsigned32), ("IsRequired", true)),
                DomainTestData.Create<PlcDeviceRuntimeStateEntity>(("PlcDeviceMapping", mapping), ("ConnectivityState", ConnectivityStateEnum.Disconnected)),
                DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "DELETED"), ("Name", "Deleted"), ("IsDeleted", true)),
                DomainTestData.Create<ProductEntity>(("Code", "SKU"), ("Name", "Product"), ("DefaultUom", "chai")),
                DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S1"), ("Name", "Ca sáng"), ("StartTime", new TimeOnly(6, 0)), ("EndTime", new TimeOnly(14, 0))));
            await origin.SaveChangesAsync();
            await new MasterDataTransfer(origin).ExportAsync(path, default);
            var snapshot = JsonSerializer.Deserialize<MasterDataSnapshot>(await File.ReadAllTextAsync(path))!;
            Assert.Equal(14, snapshot.Tables.Count);
            Assert.False(snapshot.Tables.ContainsKey("PlcDeviceRuntimeState"));
            Assert.False(snapshot.Tables.ContainsKey("Users"));
            Assert.Single(snapshot.Tables["WorkUnit"]);
            Assert.False(snapshot.Tables["WorkUnit"][0].TryGetProperty("RowVersion", out _));
            var broken = JsonSerializer.Deserialize<MasterDataSnapshot>(await File.ReadAllTextAsync(path))!;
            var invalidUnit = broken.Tables["WorkUnit"][0].Deserialize<Dictionary<string, JsonElement>>()!;
            invalidUnit["WorkCenterId"] = JsonSerializer.SerializeToElement(Guid.NewGuid());
            broken.Tables["WorkUnit"][0] = JsonSerializer.SerializeToElement(invalidUnit);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(broken));
            await Assert.ThrowsAsync<DbUpdateException>(() => new MasterDataTransfer(target).ImportAsync(path, default));
            Assert.Empty(await target.Enterprises.ToArrayAsync());
            Assert.Empty(await target.OpcUaConnections.ToArrayAsync());
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot));
            await new MasterDataTransfer(target).ImportAsync(path, default);
            Assert.Equal(unit.Id, (await target.WorkUnits.SingleAsync()).Id);
            Assert.Equal(center.Id, (await target.WorkUnits.SingleAsync()).WorkCenterId);
            Assert.Equal("Mô tả", (await target.Enterprises.SingleAsync()).Description);
            Assert.Equal(mapping.Id, (await target.PlcDeviceMappings.SingleAsync()).Id);
            Assert.Equal(2, await target.OpcUaTagMappings.CountAsync());
            Assert.Empty(await target.PlcDeviceRuntimeStates.ToArrayAsync());
            Assert.NotEmpty((await target.WorkUnits.SingleAsync()).RowVersion);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new MasterDataTransfer(target).ImportAsync(path, default));
            Assert.Single(await target.WorkUnits.ToArrayAsync());
        }
        finally
        {
            await origin.Database.EnsureDeletedAsync();
            await target.Database.EnsureDeletedAsync();
            File.Delete(path);
            SqlConnection.ClearAllPools();
        }
    }
}
