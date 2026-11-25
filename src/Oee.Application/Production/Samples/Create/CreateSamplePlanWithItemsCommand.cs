using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateSamplePlanWithItems.</summary>
public sealed record CreateSamplePlanWithItemsCommand(SamplePlanBulkCreateRequest Request)
    : ICommand<SamplePlanBulkResult>;
