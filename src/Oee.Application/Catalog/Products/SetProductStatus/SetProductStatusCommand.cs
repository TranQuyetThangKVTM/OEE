using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Catalog.Products.SetProductStatus;

/// <summary>Dữ liệu yêu cầu cho thao tác SetProductStatus.</summary>
public sealed record SetProductStatusCommand(
    Guid Id,
    string RowVersion,
    string TargetStatus
    )
    : ICommand<MasterMutationResult>;
