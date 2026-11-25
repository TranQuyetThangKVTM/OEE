using Oee.Application.Common;

namespace Oee.Application.Organizations.Areas.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteArea.</summary>
public sealed record DeleteAreaCommand(
    Guid Id,
    string RowVersion) : ICommand;
