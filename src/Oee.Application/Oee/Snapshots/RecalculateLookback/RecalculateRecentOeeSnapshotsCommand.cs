using Oee.Application.Common;

namespace Oee.Application.Oee.Snapshots.RecalculateLookback;

/// <summary>Dữ liệu yêu cầu cho thao tác RecalculateRecentOeeSnapshots.</summary>
public sealed record RecalculateRecentOeeSnapshotsCommand(TimeSpan Lookback) : ICommand<int>;
