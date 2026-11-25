using Oee.Application.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.UnitTests;

public sealed class EntityTypeNameTests
{
    [Theory]
    [InlineData(typeof(EnterpriseEntity), "Enterprise")]
    [InlineData(typeof(WorkUnitEntity), "WorkUnit")]
    [InlineData(typeof(PlcDeviceMappingEntity), "PlcDeviceMapping")]
    [InlineData(typeof(AuditLogEntity), "AuditLog")]
    public void RenamedDomainEntitiesKeepExistingAuditAndExportCodes(Type type, string expected)
    {
        Assert.Equal(expected, EntityTypeName.Get(type));
    }

    [Fact]
    public void UnrelatedTypesKeepTheirFullName()
    {
        Assert.Equal("UnrelatedEntity", EntityTypeName.Get(typeof(UnrelatedEntity)));
    }

    [Fact]
    public void MissingTypeIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => EntityTypeName.Get(null!));
    }

    private sealed class UnrelatedEntity;
}
