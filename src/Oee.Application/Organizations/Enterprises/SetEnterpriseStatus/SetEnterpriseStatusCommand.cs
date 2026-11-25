using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Enterprises.SetEnterpriseStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetEnterpriseStatus.</summary>
public sealed record SetEnterpriseStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
