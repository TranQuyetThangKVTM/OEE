using Oee.Application.Common;
using Oee.Application.Operations;
using Oee.Application.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.Create;
using Oee.Application.PlcIntegration.Configuration.Connections.Delete;
using Oee.Application.PlcIntegration.Configuration.Connections.TestConnection;
using Oee.Application.Security;

namespace Oee.Application.UnitTests;

public sealed class PlcConfigurationAuthorizationTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("delete")]
    [InlineData("test")]
    public async Task AccountWithoutPlcManageIsRejectedBeforePortsOrPayloadAreUsed(string operation)
    {
        var requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?>
        {
            [typeof(IPlcConfigurationStore)] = null,
            [typeof(IMasterDataStore)] = null,
            [typeof(IApplicationUnitOfWork)] = null,
            [typeof(IClock)] = null,
            [typeof(IOpcUaClientAdapter)] = null,
            [typeof(ICapabilityChecker)] = new DeniedCapabilities(),
        });
        Func<Task> invoke = operation switch
        {
            "create" => () => requests.ExecuteAsync(new CreateOpcUaConnectionCommand(null!)),
            "delete" => () => requests.ExecuteAsync(new DeleteOpcUaConnectionCommand(Guid.NewGuid(), "")),
            _ => () => requests.ExecuteAsync(new TestOpcUaConnectionCommand(Guid.NewGuid())),
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(invoke);
    }

    private sealed class DeniedCapabilities : ICapabilityChecker
    {
        public bool HasCapability(string capability) => false;
    }
}
