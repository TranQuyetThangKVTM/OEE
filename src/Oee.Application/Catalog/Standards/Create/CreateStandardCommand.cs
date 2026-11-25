using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Catalog.Standards.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateStandard.</summary>
public sealed record CreateStandardCommand(
    Guid WorkUnitId,
    Guid ProductId,
    decimal SpeedPerMinute,
    string Uom,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? ApprovedBy
    )
    : ICommand<MasterMutationResult>;
