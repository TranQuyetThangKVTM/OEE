using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Oee.Targets.AddTargetVersion;

/// <summary>Dữ liệu yêu cầu cho thao tác AddTargetVersion.</summary>
public sealed record AddTargetVersionCommand(EffectiveTargetRequest Request) : ICommand<Guid>;
