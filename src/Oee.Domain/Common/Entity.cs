namespace Oee.Domain.Common;

/// <summary>Định danh dùng chung cho các entity của hệ thống.</summary>
public abstract class Entity
{
    /// <summary>Khóa do ứng dụng tạo bằng UUID phiên bản 7; lớp kế thừa có thể gán lại khi cần.</summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

