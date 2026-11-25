using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Oee.Application.Common;
using Oee.Application.Production.LineOff;
using Oee.Application.Security;
using Oee.Infrastructure.Production.Automation;
using Oee.Infrastructure.Production.LineOff;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class LineOffCalendarTests
{
    [Fact]
    public async Task CalendarEnforcesPermissionsOverlapRevisionAndProductionThenSurvivesRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), "OeeLineOffTests", Guid.NewGuid().ToString("N"));
        var scopeStore = new ScopeStore();
        var checker = new Checker();
        var store = new JsonLineOffCalendarStore(Options.Create(new PlanAutomationStorageOptions { StoragePath = path }));
        var services = new ServiceCollection().AddLogging().AddOeeInfrastructure("", includeIdentityStore: false);
        services.AddSingleton<ILineOffCalendarStore>(store);
        services.AddSingleton<ILineOffScopeStore>(scopeStore);
        services.AddSingleton<ICapabilityChecker>(checker);
        services.AddSingleton<ICurrentActor>(new Actor());
        await using var provider = services.BuildServiceProvider();
        async Task<T> Execute<T>(IRequest<T> request)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(request);
        }
        try
        {
            var empty = await Execute(new GetLineOffCalendarQuery());
            Assert.Equal(Guid.Empty, empty.Revision);
            Assert.False(Directory.Exists(path));
            var command = new SaveLineOffPeriodCommand(scopeStore.LineId, new(2026, 11, 17, 10, 0, 0),
                new(2026, 11, 17, 14, 0, 0), "  Nghỉ nửa ca  ", Guid.Empty);
            checker.Allowed = false;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Execute(command));
            Assert.False(Directory.Exists(path));
            checker.Allowed = true;
            scopeStore.HasOutput = true;
            await Assert.ThrowsAsync<ArgumentException>(() => Execute(command));
            scopeStore.HasOutput = false;
            var saved = await Execute(command);
            var entry = Assert.Single(saved.Periods);
            Assert.Equal(new DateTimeOffset(2026, 11, 17, 3, 0, 0, TimeSpan.Zero), entry.Start);
            Assert.Equal("Nghỉ nửa ca", entry.Note);
            Assert.Equal("planner", entry.UpdatedBy);
            await Assert.ThrowsAsync<ArgumentException>(() => Execute(command)); // stale revision
            await Assert.ThrowsAsync<ArgumentException>(() => Execute(command with { ExpectedRevision = saved.Revision })); // overlap
            var restarted = new JsonLineOffCalendarStore(Options.Create(new PlanAutomationStorageOptions { StoragePath = path }));
            Assert.Equal(entry, Assert.Single((await restarted.ReadAsync(default)).Periods));
            var updated = await Execute(command with
            {
                ExpectedRevision = saved.Revision, Id = entry.Id,
                EndLocal = new(2026, 11, 18, 6, 0, 0)
            });
            Assert.Equal(20, (Assert.Single(updated.Periods).End - entry.Start).TotalHours);
            checker.Allowed = false;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Execute(new DeleteLineOffPeriodCommand(entry.Id, updated.Revision)));
            checker.Allowed = true;
            var deleted = await Execute(new DeleteLineOffPeriodCommand(entry.Id, updated.Revision));
            Assert.True(Assert.Single(deleted.Periods).Deleted);
            Assert.True(Assert.Single((await restarted.ReadAsync(default)).Periods).Deleted);
            Assert.Empty(Directory.GetFiles(path, "*.tmp"));
        }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OeeLineOffTests")) + Path.DirectorySeparatorChar,
                Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    private sealed class ScopeStore : ILineOffScopeStore
    {
        public Guid LineId { get; } = Guid.NewGuid();
        public bool HasOutput { get; set; }
        public Task<LineOffScope?> FindAsync(Guid workCenterId, CancellationToken cancellationToken)
            => Task.FromResult<LineOffScope?>(workCenterId == LineId ? new(LineId, Guid.NewGuid(), "Asia/Ho_Chi_Minh") : null);
        public Task<bool> HasProductionAsync(Guid fillerId, DateTimeOffset start, DateTimeOffset until, CancellationToken cancellationToken)
            => Task.FromResult(HasOutput);
    }
    private sealed class Checker : ICapabilityChecker
    {
        public bool Allowed { get; set; } = true;
        public bool HasCapability(string capability) => Allowed && capability == Capabilities.ProductionPlanManage;
    }
    private sealed class Actor : ICurrentActor
    {
        public string? UserId => "planner-id";
        public string UserName => "planner";
        public string? IpAddress => null;
    }
}
