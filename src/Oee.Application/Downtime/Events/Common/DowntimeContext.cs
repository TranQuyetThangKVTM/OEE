using Oee.Application.Common;
using Oee.Application.Operations;
using Oee.Application.Security;
using Oee.Domain.Downtime;

namespace Oee.Application.Downtime.Events.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác Downtime.</summary>
internal sealed class DowntimeContext(
    IDowntimeStore downtimes,
    IApplicationUnitOfWork unitOfWork,
    IClock clock,
    ICapabilityChecker? capabilityChecker = null)
    : IUseCaseContext
{
    internal IDowntimeStore Downtimes => downtimes;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal IClock Clock => clock;

    internal void EnsureAuthorized()
    {
        if (capabilityChecker is not null && !capabilityChecker.HasCapability(Capabilities.DowntimeWrite))
        {
            throw new UnauthorizedAccessException("The current user is not authorized to manage downtime events.");
        }
    }

    internal async Task<DowntimeEventEntity> FindAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await downtimes.FindAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Downtime was not found.");

    internal async Task EnsureNoOverlapAsync(
        Guid workUnitId,
        DateTimeOffset start,
        DateTimeOffset? end,
        CancellationToken cancellationToken)
    {
        // Event mở được xem như kéo dài vô hạn khi kiểm tra giao nhau.
        var effectiveEnd = end ?? DateTimeOffset.MaxValue;

        if (await downtimes.HasOverlapAsync(workUnitId, start, effectiveEnd, cancellationToken))
            throw new InvalidOperationException("Downtime overlaps an existing event.");
    }

    internal void ApplyConcurrency(DowntimeEventEntity item, string token)
    {
        // UpdatedAt đóng vai trò concurrency token; chuỗi "initial" đại diện giá trị null lúc mới tạo.
        DateTimeOffset? expected = token switch
        {
            "initial" => null,
            _ when DateTimeOffset.TryParse(
                token,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var value) => value,
            _ => throw new ArgumentException("Invalid downtime concurrency token.", nameof(token)),
        };
        unitOfWork.ExpectUpdatedAt(item, expected);
    }
}
