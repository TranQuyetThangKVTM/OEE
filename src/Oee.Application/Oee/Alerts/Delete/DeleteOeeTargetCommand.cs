using Oee.Application.Common;

namespace Oee.Application.Oee.Alerts.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteOeeTarget.</summary>
public sealed record DeleteOeeTargetCommand(
    Guid Id,
    string RowVersion) : ICommand;
