using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Catalog.Standards.EndStandardEffectiveness;

/// <summary>Dữ liệu yêu cầu cho thao tác EndStandardEffectiveness.</summary>
public sealed record EndStandardEffectivenessCommand(
    Guid Id,
    DateOnly EffectiveTo,
    string RowVersion
    )
    : ICommand<MasterMutationResult>;
