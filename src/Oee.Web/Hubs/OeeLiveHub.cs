using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Oee.Application.Contracts.Api;
using Oee.Web.Realtime;

namespace Oee.Web.Hubs;

/// <summary>Typed SignalR hub phát trạng thái OEE live cho client đã đăng nhập.</summary>
/// <remarks>Hub chỉ truyền DTO qua IOeeLiveClient, không gửi EF Entity/navigation property.</remarks>
[Authorize]
public sealed class OeeLiveHub(RealtimeOeeState state) : Hub<IOeeLiveClient>
{
    /// <summary>Khi client vừa kết nối, gửi ngay snapshot gần nhất trước khi nhận event tiếp theo.</summary>
    public override async Task OnConnectedAsync()
    {
        if (state.Current is { } current) await Clients.Caller.LiveOeeUpdated(current);
        await base.OnConnectedAsync();
    }
}
