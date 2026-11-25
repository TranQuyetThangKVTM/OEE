using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Oee.Targets.AddStandardVersion;

/// <summary>Dữ liệu yêu cầu cho thao tác AddStandardVersion.</summary>
public sealed record AddStandardVersionCommand(EffectiveStandardRequest Request) : ICommand<Guid>;
