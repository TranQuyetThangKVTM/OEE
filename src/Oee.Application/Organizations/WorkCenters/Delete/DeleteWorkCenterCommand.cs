using Oee.Application.Common;

namespace Oee.Application.Organizations.WorkCenters.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteWorkCenter.</summary>
public sealed record DeleteWorkCenterCommand(
    Guid Id,
    string RowVersion) : ICommand;
