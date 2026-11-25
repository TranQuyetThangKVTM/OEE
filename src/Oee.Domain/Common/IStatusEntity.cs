namespace Oee.Domain.Common;

/// <summary>Hành vi trạng thái của danh mục; Domain dùng enum, persistence chuyển sang int khi lưu.</summary>
public interface IStatusEntity
{
    RecordStatusEnum Status { get; }
    void ChangeStatus(RecordStatusEnum status);
}
