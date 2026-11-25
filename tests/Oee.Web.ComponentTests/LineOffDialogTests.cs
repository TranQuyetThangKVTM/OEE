using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Common;
using Oee.Application.Production.LineOff;
using Oee.Application.Security;
using Oee.Web.Components.Pages.Dashboard.Dialogs;
using Oee.Web.Components.Pages.Production;
using Oee.Web.Components.Shared;
using Oee.Web.Security;

namespace Oee.Web.ComponentTests;

public sealed class LineOffDialogTests : RowMenuTestContext, IAsyncLifetime
{
    private static readonly Guid LineId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Executor _executor = new();
    public LineOffDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddScoped<ProductionPlanCreationRunner>();
        Services.AddScoped<ScopedPrincipalAccessor>();
        Services.AddSingleton<IUseCaseExecutor>(_executor);
        var auth = this.AddAuthorization();
        auth.SetAuthorized("planner");
        auth.SetRoles("PlanningManager");
        auth.SetPolicies(Capabilities.ProductionPlanManage);
    }
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task PopupSendsExactHalfShiftTimesAndDoesNotOfferWholeDayOrShiftExclusion()
    {
        var dialogs = await OpenAsync();
        var editor = dialogs.FindComponent<LineOffPeriodsDialog>();
        Assert.Equal(2, editor.FindComponents<MudDatePicker>().Count);
        Assert.Equal(2, editor.FindComponents<MudTimePicker>().Count);
        Assert.Empty(editor.FindComponents<MudSelect<string>>());
        editor.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu khoảng Off").Click();
        Assert.Null(_executor.Saved);
        editor.WaitForAssertion(() => Assert.Contains("Hãy nhập đầy đủ", editor.Markup, StringComparison.Ordinal));
        await editor.InvokeAsync(() => editor.FindComponents<MudTimePicker>()[0].Instance.TimeChanged.InvokeAsync(TimeSpan.FromHours(10)));
        await editor.InvokeAsync(() => editor.FindComponents<MudTimePicker>()[1].Instance.TimeChanged.InvokeAsync(TimeSpan.FromHours(14)));
        editor.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu khoảng Off").Click();
        editor.WaitForAssertion(() => Assert.NotNull(_executor.Saved));
        Assert.Equal(new DateTime(2026, 11, 17, 10, 0, 0), _executor.Saved!.StartLocal);
        Assert.Equal(new DateTime(2026, 11, 17, 14, 0, 0), _executor.Saved.EndLocal);
        Assert.Equal(LineId, _executor.Saved.WorkCenterId);
    }

    [Fact]
    public async Task RemovingOffRequiresConfirmationAndKeepsTheDisplayedRevision()
    {
        var entry = new LineOffPeriod(Guid.NewGuid(), LineId,
            new(2026, 11, 17, 3, 0, 0, TimeSpan.Zero), new(2026, 11, 17, 7, 0, 0, TimeSpan.Zero),
            "Nghỉ nửa ca", "Asia/Ho_Chi_Minh", "planner", DateTimeOffset.UtcNow);
        var revision = Guid.NewGuid();
        _executor.Calendar = new(revision, [entry]);
        var dialogs = await OpenAsync();
        var editor = dialogs.FindComponent<LineOffPeriodsDialog>();
        SelectAction(editor, "Xóa");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<ConfirmDialog>()));
        dialogs.FindComponent<ConfirmDialog>().FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.Null(_executor.Deleted);
        SelectAction(editor, "Xóa");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<ConfirmDialog>()));
        dialogs.FindComponent<ConfirmDialog>().FindAll("button").Single(x => x.TextContent.Trim() == "Xóa").Click();
        editor.WaitForAssertion(() => Assert.NotNull(_executor.Deleted));
        Assert.Equal(new DeleteLineOffPeriodCommand(entry.Id, revision), _executor.Deleted);
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync()
    {
        var dialogs = Render<MudDialogProvider>();
        var parameters = new DialogParameters<LineOffPeriodsDialog>
        {
            { x => x.WorkCenterId, LineId }, { x => x.Line, "LINE 1" },
            { x => x.TimeZoneId, "Asia/Ho_Chi_Minh" }, { x => x.Date, new DateOnly(2026, 11, 17) }
        };
        await Services.GetRequiredService<IDialogService>().ShowAsync<LineOffPeriodsDialog>("Thời gian Off", parameters);
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<LineOffPeriodsDialog>()));
        dialogs.WaitForAssertion(() => Assert.False(dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu khoảng Off").HasAttribute("disabled")));
        return dialogs;
    }

    private sealed class Executor : IUseCaseExecutor
    {
        public LineOffCalendar Calendar { get; set; } = LineOffCalendar.Empty;
        public SaveLineOffPeriodCommand? Saved { get; private set; }
        public DeleteLineOffPeriodCommand? Deleted { get; private set; }
        public Task<TResult> ExecuteAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
        {
            if (request is SaveLineOffPeriodCommand save) Saved = save;
            if (request is DeleteLineOffPeriodCommand delete) Deleted = delete;
            return Task.FromResult((TResult)(object)Calendar);
        }
        public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
