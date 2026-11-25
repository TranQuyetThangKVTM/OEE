using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.Common;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class SampleProductionPlanValidator
{
    internal static void Validate(SamplePlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 32)
            throw new ArgumentException("Sample-plan code is required and cannot exceed 32 characters.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new ArgumentException("Sample-plan name is required and cannot exceed 200 characters.", nameof(request));

        if (request.Description?.Length > 1000)
            throw new ArgumentException("Description cannot exceed 1000 characters.", nameof(request));
    }

    internal static void Validate(SamplePlanItemRequest request)
    {
        if (request.WorkUnitId == Guid.Empty || request.ProductId == Guid.Empty || request.ShiftId == Guid.Empty)
            throw new ArgumentException("Work unit, product and shift are required.", nameof(request));

        if (request.PlannedQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Planned quantity must be greater than zero.");

        if (request.Notes?.Length > 500)
            throw new ArgumentException("Notes cannot exceed 500 characters.", nameof(request));
    }

    internal static void ValidateBulk(
        string name,
        string? description,
        IReadOnlyCollection<Guid> workUnitIds,
        IReadOnlyCollection<Guid> shiftIds,
        Guid productId,
        decimal quantity,
        string? notes,
        bool validateName = true)
    {
        if (validateName && (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200))
            throw new ArgumentException("Tên kế hoạch mẫu là bắt buộc và không vượt quá 200 ký tự.", nameof(name));

        if (description?.Length > 1000 || notes?.Length > 500)
            throw new ArgumentException("Mô tả hoặc ghi chú vượt quá độ dài cho phép.");

        if (!workUnitIds.Any(x => x != Guid.Empty)
            || !shiftIds.Any(x => x != Guid.Empty)
            || productId == Guid.Empty
            || quantity <= 0)
            throw new ArgumentException("Phải chọn thiết bị, ca, SKU và sản lượng lớn hơn 0.");
    }
}
