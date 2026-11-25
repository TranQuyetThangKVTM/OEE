using Oee.Application.Common;

namespace Oee.Application.Organizations.WorkUnits.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteWorkUnit.</summary>
public sealed record DeleteWorkUnitCommand(
    Guid Id,
    string RowVersion) : ICommand;
