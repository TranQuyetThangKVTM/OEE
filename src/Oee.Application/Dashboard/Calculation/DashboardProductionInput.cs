namespace Oee.Application.Modules;

/// <summary>Một dòng sản lượng đầu vào dùng để cộng gộp Performance và Quality.</summary>
/// <param name = "TotalQuantity">Tổng số lượng sản xuất, gồm cả tốt và lỗi.</param>
/// <param name = "DefectQuantity">Số lượng lỗi; không được lớn hơn tổng sản lượng.</param>
/// <param name = "IdealRunMinutes">Thời gian chạy lý tưởng để tạo ra tổng sản lượng, đơn vị phút.</param>
public sealed record DashboardProductionInput(
    decimal TotalQuantity,
    decimal DefectQuantity,
    decimal IdealRunMinutes);
