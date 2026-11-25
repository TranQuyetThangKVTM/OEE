using Oee.Application.Contracts.Operations;
using Oee.Domain.Common;

namespace Oee.Application.Operations;

/// <summary>Chuẩn hóa giá trị và lỗi dùng chung của các command master; không giữ DbContext hoặc thực hiện CRUD.</summary>
internal static class MasterDataValidation
{
    internal static (
        Guid ParentId,
        string Code,
        string Name,
        RecordStatusEnum Status,
        string? Description)
        ValidateScoped(
        Guid parentId,
        string code,
        string name,
        string status,
        string parentName,
        string? description)
    {
        if (parentId == Guid.Empty)
            throw new ArgumentException($"{parentName} is required.");

        return (
            parentId,
            Required(code, 32, "Code"),
            Required(name, 200, "Name"),
            Status(status),
            Optional(description, 500, "Description"));
    }

    // Trim trước khi kiểm tra giúp code/name không khác nhau chỉ bởi khoảng trắng đầu/cuối.
    internal static string Required(
        string? value,
        int max,
        string name) => DomainGuard.Required(value, max, name);

    internal static string? Optional(
        string? value,
        int max,
        string name) => DomainGuard.Optional(value, max, name);

    internal static RecordStatusEnum Status(string value)
        => Enum.TryParse<RecordStatusEnum>(value, true, out var status) && Enum.IsDefined(status)
            ? status
            : throw new ArgumentException("Record status is invalid.");

    internal static InvalidOperationException ActiveDependencies(
        string entity,
        string dependencies)
        => new($"{entity} has {dependencies}. Deactivate those dependencies first; status is never cascaded automatically.");

    internal static MasterMutationResult Result(SoftDeletableAuditedEntity entity)
        => new(entity.Id, Convert.ToBase64String(entity.RowVersion));

    internal static KeyNotFoundException Missing(string name) => new($"{name} was not found.");

    internal static InvalidOperationException Referenced(string name)
        => new($"{name} is referenced by historical or child data and cannot be deleted. Set it to Inactive instead.");
}
