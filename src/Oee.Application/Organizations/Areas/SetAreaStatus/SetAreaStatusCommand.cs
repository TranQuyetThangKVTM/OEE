using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Areas.SetAreaStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetAreaStatus.</summary>
public sealed record SetAreaStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
