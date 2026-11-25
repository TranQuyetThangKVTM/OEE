using Oee.Application.Contracts.Operations;
using Oee.Application.Catalog.Products.Create;
using Oee.Application.Catalog.Products.SetProductStatus;
using Oee.Application.Catalog.Products.Update;
using Oee.Application.Catalog.Standards.Create;
using Oee.Application.Catalog.Standards.EndStandardEffectiveness;
using Oee.Application.Downtime.Reasons.Create;
using Oee.Application.Downtime.Reasons.SetDowntimeReasonStatus;
using Oee.Application.Downtime.Reasons.Update;
using Oee.Application.Organizations.Areas.Create;
using Oee.Application.Organizations.Areas.SetAreaStatus;
using Oee.Application.Organizations.Areas.Update;
using Oee.Application.Organizations.Enterprises.Create;
using Oee.Application.Organizations.Enterprises.SetEnterpriseStatus;
using Oee.Application.Organizations.Enterprises.Update;
using Oee.Application.Organizations.Shifts.Create;
using Oee.Application.Organizations.Shifts.SetShiftStatus;
using Oee.Application.Organizations.Shifts.Update;
using Oee.Application.Organizations.Sites.Create;
using Oee.Application.Organizations.Sites.SetSiteStatus;
using Oee.Application.Organizations.Sites.Update;
using Oee.Application.Organizations.WorkCenters.Create;
using Oee.Application.Organizations.WorkCenters.SetWorkCenterStatus;
using Oee.Application.Organizations.WorkCenters.Update;
using Oee.Application.Organizations.WorkUnits.Create;
using Oee.Application.Organizations.WorkUnits.SetWorkUnitStatus;
using Oee.Application.Organizations.WorkUnits.Update;

namespace Oee.Tests.UseCases;
/// <summary>
/// Tập use case ghi Master Data và cơ cấu tổ chức.
/// </summary>
/// <remarks>
/// Các overload nhận DTO update chuyên biệt cho sửa Code của năm cấp tổ chức và giữ quan hệ cha chỉ đọc;
/// RowVersion giúp phát hiện người khác đã sửa bản ghi. Implementation ở Infrastructure kiểm tra
/// unique/effectivity/FK, lưu SQL và audit. Razor chỉ gọi contract này, không inject DbContext.
/// </remarks>
public interface IMasterDataScenario
{
    /// <summary>Xóa mềm tối đa 200 dòng đã chọn, con trước cha, tất cả thành công hoặc rollback toàn bộ.</summary>
    Task DeleteHierarchyAsync(IReadOnlyCollection<EquipmentHierarchyDeleteItem> items, CancellationToken cancellationToken);
    // Product: tạo/cập nhật dữ liệu cho phép, đổi trạng thái hoặc xóa mềm theo rule tham chiếu.
    Task<MasterMutationResult> CreateProductAsync(CreateProductCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateProductAsync(UpdateProductCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetProductStatusAsync(SetProductStatusCommand request, CancellationToken cancellationToken);
    Task DeleteProductAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // WorkUnitStandard là dữ liệu có phiên bản hiệu lực; đổi tốc độ phải bảo toàn lịch sử.
    Task<MasterMutationResult> CreateStandardAsync(CreateStandardCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> EndStandardEffectivenessAsync(EndStandardEffectivenessCommand request, CancellationToken cancellationToken);
    Task DeleteStandardAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // Shift lưu giờ địa phương bằng TimeOnly và được Site cung cấp timezone khi resolve period.
    Task<MasterMutationResult> CreateShiftAsync(CreateShiftCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateShiftAsync(UpdateShiftCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetShiftStatusAsync(SetShiftStatusCommand request, CancellationToken cancellationToken);
    Task DeleteShiftAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // DowntimeReason là danh mục cho sự kiện tương lai; update không tự reclassify lịch sử.
    Task<MasterMutationResult> CreateDowntimeReasonAsync(CreateDowntimeReasonCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateDowntimeReasonAsync(UpdateDowntimeReasonCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetDowntimeReasonStatusAsync(SetDowntimeReasonStatusCommand request, CancellationToken cancellationToken);
    Task DeleteDowntimeReasonAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // WorkUnit là máy/dây chuyền; mutation không được làm mất production/downtime/PLC history.
    Task<MasterMutationResult> CreateWorkUnitAsync(CreateWorkUnitCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateWorkUnitAsync(UpdateWorkUnitCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetWorkUnitStatusAsync(SetWorkUnitStatusCommand request, CancellationToken cancellationToken);
    Task DeleteWorkUnitAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // WorkCenter nằm dưới Area; command phải kiểm tra parent còn active.
    Task<MasterMutationResult> CreateWorkCenterAsync(CreateWorkCenterCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateWorkCenterAsync(UpdateWorkCenterCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetWorkCenterStatusAsync(SetWorkCenterStatusCommand request, CancellationToken cancellationToken);
    Task DeleteWorkCenterAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // Area nằm dưới Site và không được di chuyển Site bằng update thông thường.
    Task<MasterMutationResult> CreateAreaAsync(CreateAreaCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateAreaAsync(UpdateAreaCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetAreaStatusAsync(SetAreaStatusCommand request, CancellationToken cancellationToken);
    Task DeleteAreaAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // Enterprise là nút gốc của cây tổ chức; không cascade trạng thái/xóa xuống Site con.
    Task<MasterMutationResult> CreateEnterpriseAsync(CreateEnterpriseCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateEnterpriseAsync(UpdateEnterpriseCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetEnterpriseStatusAsync(SetEnterpriseStatusCommand request, CancellationToken cancellationToken);
    Task DeleteEnterpriseAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    // Site chứa TimeZoneId dùng để chuyển UTC sang giờ vận hành và resolve ca.
    Task<MasterMutationResult> CreateSiteAsync(CreateSiteCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> UpdateSiteAsync(UpdateSiteCommand request, CancellationToken cancellationToken);
    Task<MasterMutationResult> SetSiteStatusAsync(SetSiteStatusCommand request, CancellationToken cancellationToken);
    Task DeleteSiteAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
}
