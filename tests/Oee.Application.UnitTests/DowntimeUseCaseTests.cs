using System.Data;
using System.Globalization;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Platform;

namespace Oee.Application.UnitTests;

public sealed class DowntimeUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeniedCommandDoesNotReadOrSaveData()
    {
        var store = new MemoryDowntimeStore(); var unit = new RecordingUnit();
        var service = new DowntimeScenario(store, unit, new FixedClock(), new DeniedCapabilities());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(Request(store), default));
        Assert.Equal(0, store.Reads); Assert.Equal(0, unit.Saves); Assert.Null(store.Added);
    }

    [Fact]
    public async Task CreateCopiesReasonClassificationAndSavesOnce()
    {
        var store = new MemoryDowntimeStore(); var unit = new RecordingUnit();
        var request = Request(store);
        var id = await new DowntimeScenario(store, unit, new FixedClock()).CreateAsync(request, default);
        Assert.NotNull(store.Added); Assert.Equal(id, store.Added.Id);
        Assert.Equal(store.Reason.Category, store.Added.Category); Assert.Equal(store.Reason.Kind, store.Added.Kind);
        Assert.Equal(request.StartAt, store.Added.StartAt); Assert.Equal(DataSourceEnum.Manual, store.Added.Source);
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task ClosePassesOriginalTokenToPersistenceAndUsesDomainRule()
    {
        var store = new MemoryDowntimeStore(); var unit = new RecordingUnit();
        var prior = Now.AddMinutes(-10); store.Event.RecordUpdate(prior);
        await new DowntimeScenario(store, unit, new FixedClock()).CloseAsync(store.Event.Id, Now,
            prior.ToString("O", CultureInfo.InvariantCulture), default);
        Assert.Equal(prior, unit.ExpectedUpdatedAt); Assert.Equal(Now, store.Event.EndAt);
        Assert.Equal(Now, store.Event.UpdatedAt); Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task InvalidCloseDoesNotSaveOrChangeEndTime()
    {
        var store = new MemoryDowntimeStore(); var unit = new RecordingUnit();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new DowntimeScenario(store, unit, new FixedClock())
            .CloseAsync(store.Event.Id, store.Event.StartAt.AddMinutes(-1), "initial", default));
        Assert.Null(store.Event.EndAt); Assert.Equal(0, unit.Saves);
    }

    [Fact]
    public async Task BulkMissingEventDoesNotPartiallyReclassify()
    {
        var store = new MemoryDowntimeStore(); var unit = new RecordingUnit(); var before = store.Event.DowntimeReasonId;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new DowntimeScenario(store, unit, new FixedClock()).BulkReclassifyAsync(
            [new(store.Event.Id, "initial"), new(Guid.NewGuid(), "initial")], store.Reason.Id, default));
        Assert.Equal(before, store.Event.DowntimeReasonId); Assert.Equal(0, unit.Saves);
    }

    private static DowntimeRequest Request(MemoryDowntimeStore store) => new(Guid.NewGuid(), store.Reason.Id, Now.AddHours(-1), Now, null, "manual");
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class DeniedCapabilities : ICapabilityChecker { public bool HasCapability(string capability) => false; }
    private sealed class MemoryDowntimeStore : IDowntimeStore
    {
        public int Reads { get; private set; }
        public DowntimeEventEntity? Added { get; private set; }
        public DowntimeReasonEntity Reason { get; } = DowntimeReasonEntity.Create("BREAK", "Breakdown", DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown);
        public DowntimeEventEntity Event { get; } = DowntimeEventEntity.Create(Guid.NewGuid(), Guid.NewGuid(),
            DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown, Now.AddHours(-1), DataSourceEnum.Manual);
        public Task<DowntimeEventEntity?> FindAsync(Guid id, CancellationToken ct) { Reads++; return Task.FromResult<DowntimeEventEntity?>(id == Event.Id ? Event : null); }
        public Task<DowntimeEventEntity[]> ReadAsync(Guid[] ids, CancellationToken ct) { Reads++; return Task.FromResult(ids.Contains(Event.Id) ? new[] { Event } : []); }
        public Task<DowntimeReasonEntity?> FindReasonAsync(Guid id, CancellationToken ct) { Reads++; return Task.FromResult<DowntimeReasonEntity?>(id == Reason.Id ? Reason : null); }
        public Task<bool> HasOpenAsync(Guid unitId, CancellationToken ct) { Reads++; return Task.FromResult(false); }
        public Task<bool> HasOverlapAsync(Guid unitId, DateTimeOffset start, DateTimeOffset endAt, CancellationToken ct) { Reads++; return Task.FromResult(false); }
        public void Add(DowntimeEventEntity item) => Added = item;
    }
    private sealed class RecordingUnit : IApplicationUnitOfWork
    {
        public int Saves { get; private set; }
        public DateTimeOffset? ExpectedUpdatedAt { get; private set; }
        public bool HasChanges => false;
        public Task<IApplicationTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) { Saves++; return Task.FromResult(1); }
        public void ExpectRowVersion(SoftDeletableAuditedEntity entity, byte[] expected) => throw new NotSupportedException();
        public void ExpectUpdatedAt(AuditedEntity entity, DateTimeOffset? expected) => ExpectedUpdatedAt = expected;
        public void RequireUpdatedAtWrite(AuditedEntity entity) => throw new NotSupportedException();
        public void AddAudit(AuditLogEntity entry) => throw new NotSupportedException();
        public void DiscardChanges() => throw new NotSupportedException();
    }
}
