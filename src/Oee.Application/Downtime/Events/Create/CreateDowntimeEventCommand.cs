using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Downtime.Events.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateDowntimeEvent.</summary>
public sealed record CreateDowntimeEventCommand(DowntimeRequest Request) : ICommand<Guid>;
