using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.Enterprises.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Enterprises.Create;

/// <summary>
/// Điều phối việc tạo doanh nghiệp trong cùng scope lưu dữ liệu.
/// </summary>
internal sealed class CreateEnterpriseHandler(EnterpriseContext context)
    : IRequestHandler<CreateEnterpriseCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateEnterpriseCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateEnterpriseValidator.ValidateEnterprise(command);
        await context.EnsureUniqueEnterpriseCodeAsync(values.Code, null, cancellationToken);

        var entity = EnterpriseEntity.Create(
            code: values.Code,
            name: values.Name,
            description: values.Description,
            status: values.Status);

        context.store.AddEnterprise(entity);
        await context.Context.SaveCreateAsync("Enterprise", cancellationToken);

        return Result(entity);
    }
}
