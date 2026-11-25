using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Connections.TestConnection;

/// <summary>Điều phối thao tác TestOpcUaConnection trong cùng scope lưu dữ liệu.</summary>
internal sealed class TestOpcUaConnectionHandler(PlcConnectionContext context)
    : IRequestHandler<TestOpcUaConnectionCommand, OpcUaConnectionTestResult>
{
    public async Task<OpcUaConnectionTestResult> HandleAsync(
        TestOpcUaConnectionCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var id = command.Id;
        var connection = await context.store.FindConnectionAsync(id, false, cancellationToken)
            ?? throw Missing("OPC UA connection");

        try
        {
            await using var session = await context.opcUa.ConnectAsync(PlcConnectionContext.ToEndpoint(connection), cancellationToken);

            return new(
                session.IsConnected,
                session.IsConnected ? "Connected" : "Disconnected",
                session.IsConnected ? "Kết nối OPC UA thành công." : "OPC UA không xác nhận trạng thái kết nối.",
                context.clock.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Do not propagate SDK exception text: it can contain endpoint or credential details.
            return new(
                false,
                "Disconnected",
                "Không thể kết nối OPC UA. Kiểm tra endpoint, security và secret reference.",
                context.clock.UtcNow);
        }
    }
}
