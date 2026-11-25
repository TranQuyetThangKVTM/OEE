using Oee.Application.Common;

namespace Oee.Application.Catalog.Standards.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteStandard.</summary>
public sealed record DeleteStandardCommand(
    Guid Id,
    string RowVersion) : ICommand;
