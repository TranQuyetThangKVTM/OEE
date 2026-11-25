
namespace Oee.Web.Components.Pages;

public partial class MasterOrganization
{
    // Danh sách route là metadata điều hướng tĩnh; dữ liệu nghiệp vụ bên trong từng danh mục vẫn đến từ Application/SQL.
    private static readonly MasterCatalogEntry[] Entries = [new("Sản phẩm / SKU", "Danh mục sản phẩm cho kế hoạch và sản lượng.", "/master/products"), new("Định mức tốc độ", "Phiên bản định mức theo thiết bị và SKU.", "/master/work-unit-standards"), new("Ca làm việc", "Ca trong ngày và qua nửa đêm theo Site.", "/master/shifts"), new("Lý do dừng máy", "Classification và nhóm tổn thất OEE.", "/master/downtime-reasons"), new("Cây cơ cấu tổ chức", "Enterprise → Site → Area → WorkCenter → WorkUnit.", "/equipment/hierarchy")];
}
