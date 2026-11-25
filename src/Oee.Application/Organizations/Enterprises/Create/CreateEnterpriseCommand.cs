using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Organizations.Enterprises.Create;

/// <summary>
/// Dữ liệu yêu cầu tạo doanh nghiệp.
/// </summary>
public sealed record CreateEnterpriseCommand(
    string Code,
    string Name,
    string? Description,
    string Status)
    : ICommand<MasterMutationResult>;
