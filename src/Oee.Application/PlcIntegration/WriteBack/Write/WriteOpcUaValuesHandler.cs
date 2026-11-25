using Oee.Application.Common;
using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration.WriteBack.Write;

/// <summary>Điều phối thao tác WriteOpcUaValues trong cùng scope lưu dữ liệu.</summary>
internal sealed class WriteOpcUaValuesHandler(
    IOpcUaWriteBackStore store,
    IOpcUaClientAdapter adapter,
    IOpcUaWriteBackSettings settings,
    IClock clock)
    : ICommandHandler<WriteOpcUaValuesCommand>
{
    public async Task HandleAsync(WriteOpcUaValuesCommand command, CancellationToken cancellationToken)
    {
        var devices = await store.ReadDevicesAsync(cancellationToken);

        foreach (var device in devices)
        {
            // Connectivity phải Connected và LastDataAt còn mới; OperationalState không được dùng thay health kết nối.
            var runtime = await store.FindRuntimeAsync(device.Id, cancellationToken);

            if (runtime is null || runtime.ConnectivityState != ConnectivityStateEnum.Connected
                || runtime.LastDataAt < clock.UtcNow.AddSeconds(-settings.NoDataSeconds))
                continue;
            var tags = await store.ReadWriteTagsAsync(device.Id, cancellationToken);

            if (tags.Length == 0)
                continue;
            var snapshot = await store.FindLatestSnapshotAsync(device.WorkUnitId, cancellationToken);

            if (snapshot is null)
                continue;
            var target = await store.ReadCurrentTargetAsync(device.WorkUnitId, clock.UtcNow, cancellationToken) ?? 0;
            var openStart = await store.ReadOpenDowntimeStartAsync(device.WorkUnitId, cancellationToken);
            // Khi máy chạy lại, CurrentDowntime được ghi 0. Khi dừng, giá trị là số phút của event đang mở.
            var currentDowntime = runtime.OperationalState == OperationalStateEnum.Running
                ? 0m
                : openStart.HasValue ? (decimal)(clock.UtcNow - openStart.Value).TotalMinutes : 0m;
            // Database lưu A/P/Q/OEE theo 0..1; output tag HMI nhận phần trăm 0..100.
            var values = new Dictionary<TagRoleEnum, object?>
            {
                {
                    TagRoleEnum.Oee,
                    snapshot.Oee * 100
                },
                {
                    TagRoleEnum.Availability,
                    snapshot.Availability * 100
                },
                {
                    TagRoleEnum.Performance,
                    snapshot.Performance * 100
                },
                {
                    TagRoleEnum.Quality,
                    snapshot.Quality * 100
                },
                {
                    TagRoleEnum.CurrentDowntime,
                    currentDowntime
                },
                {
                    TagRoleEnum.GoodQuantity,
                    snapshot.GoodQuantity
                },
                {
                    TagRoleEnum.TargetQuantity,
                    target
                }
            };
            var writes = tags.Where(x => values.ContainsKey(x.TagRole)).Select(x => new OpcUaWriteRequest(x.NodeId, values[x.TagRole]))
                .ToArray();

            if (writes.Length == 0)
                continue;
            var endpoint = new OpcUaEndpointOptions(
                device.OpcUaConnectionId,
                device.OpcUaConnection.EndpointUrl,
                device.OpcUaConnection.SecurityPolicy,
                device.OpcUaConnection.SecurityMode,
                device.OpcUaConnection.AuthenticationType,
                device.OpcUaConnection.UserName,
                device.OpcUaConnection.CredentialSecretReference,
                device.OpcUaConnection.CertificateReference);
            await using var session = await adapter.ConnectAsync(endpoint, cancellationToken);
            await session.WriteAsync(writes, cancellationToken);
        }
    }
}
