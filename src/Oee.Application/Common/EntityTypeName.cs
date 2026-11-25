using Oee.Domain.Common;

namespace Oee.Application.Common;

/// <summary>Giữ mã loại entity trong audit và file danh mục ổn định khi đổi tên lớp CLR.</summary>
public static class EntityTypeName
{
    public static string Get(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var name = type.Name;

        return typeof(Entity).IsAssignableFrom(type) && name.EndsWith("Entity", StringComparison.Ordinal)
            ? name[..^"Entity".Length]
            : name;
    }
}
