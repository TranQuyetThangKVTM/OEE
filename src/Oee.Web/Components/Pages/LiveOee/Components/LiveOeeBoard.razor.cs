using Microsoft.AspNetCore.Components;
using Oee.Application.Contracts.Modules;

namespace Oee.Web.Components.Pages;

public partial class LiveOeeBoard
{
    [Parameter, EditorRequired]
    public LiveOeeModel Model { get; set; } = default!;
}
