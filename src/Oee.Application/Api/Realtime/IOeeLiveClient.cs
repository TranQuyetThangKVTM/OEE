using Oee.Application.Contracts.Modules;

namespace Oee.Application.Contracts.Api;

/// <summary>Typed SignalR client contract mà Web hub gọi trên browser/mobile subscriber.</summary>
public interface IOeeLiveClient
{
    Task LiveOeeUpdated(LiveOeeModel message);
}
