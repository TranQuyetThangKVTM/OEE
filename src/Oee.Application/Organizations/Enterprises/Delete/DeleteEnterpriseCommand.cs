using Oee.Application.Common;

namespace Oee.Application.Organizations.Enterprises.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteEnterprise.</summary>
public sealed record DeleteEnterpriseCommand(
    Guid Id,
    string RowVersion) : ICommand;
