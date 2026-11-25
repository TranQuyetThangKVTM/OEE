using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Connections.Common;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Queries;

internal sealed class TestOpcUaTagHandler(PlcConnectionContext context)
    : IRequestHandler<TestOpcUaTagQuery, OpcTagDirectRead>
{
    public async Task<OpcTagDirectRead> HandleAsync(
        TestOpcUaTagQuery query,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();
        var nodeId = DomainGuard.Required(query.NodeId, 512);
        if (!Enum.TryParse<TagRoleEnum>(query.Role, out var role) || !Enum.IsDefined(role))
            throw new ArgumentException("Tag role is invalid.");

        var connection = await context.store.FindConnectionAsync(query.ConnectionId, false, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy kết nối OPC UA.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await using var session = await context.opcUa.ConnectAsync(
                PlcConnectionContext.ToEndpoint(connection), timeout.Token);
            var values = await session.ReadAsync([new(nodeId, role)], timeout.Token);
            var sample = values.SingleOrDefault(value => value.NodeId == nodeId && value.Role == role);

            return new(
                nodeId,
                OpcTagValueFormatter.Format(sample?.Value),
                sample?.Value?.GetType().Name ?? "—",
                sample is { IsGood: true, Value: not null },
                sample?.StatusCode ?? "Không nhận dữ liệu",
                sample?.SourceTimestamp,
                context.clock.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(
                nodeId, "—", "—", false,
                "Không thể đọc tag. Kiểm tra NodeId, kết nối và security.",
                null, context.clock.UtcNow);
        }
    }
}
