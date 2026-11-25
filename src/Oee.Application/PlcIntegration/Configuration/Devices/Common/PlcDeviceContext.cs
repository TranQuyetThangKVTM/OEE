using Oee.Application.Common;
using Oee.Domain.Common;
using Oee.Domain.PlcIntegration;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác PlcDevice.</summary>
internal sealed class PlcDeviceContext(PlcConfigurationContext context) : IUseCaseContext
{
    internal PlcConfigurationContext Context => context;
    internal IPlcConfigurationStore store => context.Store;

    internal static readonly TagRoleEnum[] ReadRoles = [
        TagRoleEnum.Running,
        TagRoleEnum.TotalCounter,
        TagRoleEnum.DefectCounter,
        TagRoleEnum.FaultCode,
        TagRoleEnum.ProductCode
    ];
    internal static readonly TagRoleEnum[] WriteRoles = [
        TagRoleEnum.Oee,
        TagRoleEnum.Availability,
        TagRoleEnum.Performance,
        TagRoleEnum.Quality,
        TagRoleEnum.CurrentDowntime,
        TagRoleEnum.GoodQuantity,
        TagRoleEnum.TargetQuantity,
        TagRoleEnum.Mtbf,
        TagRoleEnum.Mttr
    ];

    internal async Task ValidateDeviceReferencesAndUniquenessAsync(
        PlcDeviceValues values,
        Guid? except,
        CancellationToken cancellationToken)
    {
        // Validate FK và business key ở Application boundary; database index vẫn là lớp bảo vệ cuối khi race condition.
        var connectionId = values.ConnectionId;
        var workUnitId = values.WorkUnitId;
        var productId = values.ProductId;
        var externalId = values.ExternalId;

        if (!await store.AnyConnectionsAsync(x => x.Id == connectionId, cancellationToken))
            throw Missing("OPC UA connection");

        if (!await context.Masters.AnyWorkUnitsAsync(x => x.Id == workUnitId, cancellationToken))
            throw Missing("WorkUnit");

        if (productId.HasValue && !await context.Masters.AnyProductsAsync(x => x.Id == productId, cancellationToken))
            throw Missing("Product");

        if (await store.AnyDevicesAsync(x => x.Id != except && x.WorkUnitId == workUnitId, cancellationToken))
            throw new InvalidOperationException("WorkUnit already has a PLC mapping.");

        if (await store.AnyDevicesAsync(
            x => x.Id != except && x.OpcUaConnectionId == connectionId && x.ExternalDeviceId == externalId,
            cancellationToken))
            throw new InvalidOperationException("External device ID already exists on this connection.");
    }

    internal static OpcUaTagMappingEntity ToEntity(
        Guid mappingId,
        PlcTagValues tag)
        => OpcUaTagMappingEntity.Create(
            plcDeviceMappingId: mappingId,
            tagRole: tag.Role,
            direction: tag.Direction,
            nodeId: tag.NodeId,
            dataType: tag.DataType,
            isRequired: tag.Required);
}
