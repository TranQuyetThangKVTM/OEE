using Oee.Application.Common;

namespace Oee.Application.Organizations.Shifts.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteShift.</summary>
public sealed record DeleteShiftCommand(
    Guid Id,
    string RowVersion) : ICommand;
