using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Enterprises.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateEnterprise.</summary>
public sealed record UpdateEnterpriseCommand(
    Guid Id,
    string Name,
    string? Description,
    string RowVersion,
    string? Code = null
    )
    : ICommand<MasterMutationResult>;
