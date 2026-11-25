namespace Oee.Domain.Common;

/// <summary>Entity có audit, cờ xóa mềm và token kiểm soát cập nhật đồng thời.</summary>
/// <remarks>Lớp persistence chịu trách nhiệm cấu hình bộ lọc xóa mềm và cơ chế RowVersion.</remarks>
public abstract class SoftDeletableAuditedEntity : AuditedEntity
{
    /// <summary>Đánh dấu bản ghi đã xóa mềm; mặc định là false.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Token phiên bản do persistence cập nhật, ban đầu là mảng rỗng.</summary>
    private byte[] rowVersion = [];
    public byte[] RowVersion
    {
        get => rowVersion.ToArray();
        private set => rowVersion = value.ToArray();
    }

    public void SoftDelete(DateTimeOffset at, string? actor = null)
    {
        RecordUpdate(at, actor);
        IsDeleted = true;
    }

    protected void Restore() => IsDeleted = false;
}
