using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteSamplePlanItems.</summary>
public sealed record DeleteSamplePlanItemsCommand(IReadOnlyCollection<SamplePlanItemConcurrency> Requests)
    : ICommand;
