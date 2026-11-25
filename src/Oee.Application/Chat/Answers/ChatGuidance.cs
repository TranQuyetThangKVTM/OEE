namespace Oee.Application.Chat;

internal static class ChatGuidance
{
    internal static ChatAnswerDto Answer(ChatIntent intent)
    {
        if (intent.Kind != ChatIntentKind.HowTo)
            return new(
                intent.Kind == ChatIntentKind.Unknown
                    ? "Tôi chưa nhận diện được câu hỏi. Hãy nêu OEE, sản lượng, downtime, kế hoạch, cảnh báo hoặc mã máy và khoảng ngày. Ví dụ: OEE máy M01 7 ngày qua."
                    : "Tôi có thể tra cứu OEE/A/P/Q, sản lượng, xếp hạng máy/ca, downtime/MTBF/MTTR, kế hoạch, cảnh báo, ngưỡng SLO, ánh xạ PLC, dữ liệu nền và nhật ký thao tác.\nVí dụ: So sánh máy M01 với máy M03 tuần này; Top 5 lý do dừng máy hôm nay; OEE từ 01/10/2026 đến 15/10/2026.\nNếu có quyền quản lý kế hoạch, mở nút Lịch kế hoạch / PDF ca để chọn mẫu, giờ tạo kế hoạch ngày mai và tự xuất PDF sau mỗi ca. Câu hỏi chat không tự sửa/xóa dữ liệu; lịch chỉ hoạt động sau khi bạn bật và lưu.",
                [],
                ChatSamples.Featured,
                intent.Kind);
        var (text, title, href) = intent.Slots.Topic switch
        {
            "mapping" => (
                "Vào Ánh xạ thiết bị → Thêm mapping. Chọn WorkUnit và kết nối OPC UA, cấu hình tag đọc Running/TotalCounter và các tag khác cần thiết. Kiểm tra NodeId, kiểu dữ liệu và chế độ counter rồi Lưu. Chỉ lưu khi dữ liệu cấu hình hợp lệ.",
                "Ánh xạ thiết bị",
                "/plc/mappings"),
            "fault-code" => (
                "Vào Ánh xạ mã lỗi → Thêm. Chọn phạm vi máy (hoặc global), mã lỗi PLC và lý do dừng tương ứng rồi Lưu. Mã lỗi không xác định cần được kiểm tra và phân loại; không tự suy đoán lý do.",
                "Ánh xạ mã lỗi",
                "/plc/faults"),
            "speed-standard" => (
                "Vào Định mức tốc độ → Thêm. Chọn máy × SKU, tốc độ chuẩn theo phút, đơn vị và khoảng hiệu lực. Khi đổi định mức, kết thúc hiệu lực phiên bản cũ rồi tạo phiên bản mới để giữ số liệu quá khứ.",
                "Định mức tốc độ",
                "/master/work-unit-standards"),
            "plan" => (
                "Vào Kế hoạch sản xuất → Thêm kế hoạch hoặc Từ mẫu. Chọn máy, SKU, ca/khoảng thời gian và sản lượng kế hoạch rồi Lưu. Kế hoạch có hiệu lực sau khi lưu; trạng thái hiển thị được tính từ thời gian và sản lượng thực tế. Sửa/xóa tại cột Thao tác, theo quyền tài khoản.",
                "Kế hoạch sản xuất",
                "/production/plans"),
            "downtime-reason-update" => (
                "Vào Phân loại dừng máy. Mở sự kiện, kiểm tra máy và thời điểm rồi chọn đúng lý do/phân loại. Xác nhận cập nhật theo quyền được cấp. Muốn xem timeline theo ca, vào báo cáo Sự kiện downtime.",
                "Phân loại dừng máy",
                "/downtime"),
            "downtime-reason" => (
                "Vào Lý do dừng máy → Thêm. Nhập mã, tên, nhóm tổn thất và loại dừng, sau đó Lưu. Chọn loại Failure/Breakdown đúng thực tế vì báo cáo độ tin cậy chỉ tính các sự cố hỏng máy phù hợp.",
                "Lý do dừng máy",
                "/master/downtime-reasons"),
            "slo-target" => (
                "Vào Mục tiêu SLO. Tạo phiên bản ngưỡng theo phạm vi, loại kỳ và ngày hiệu lực; nhập OEE/A/P/Q rồi Lưu. Kiểm tra ngưỡng đang áp dụng trước khi thay đổi; giữ lịch sử bằng phiên bản hiệu lực.",
                "Mục tiêu SLO",
                "/alerts/targets"),
            "alert-handling" => (
                "Vào Cảnh báo SLO. Mở cảnh báo, xem máy/kỳ/chỉ số thực tế và ngưỡng. Nếu có quyền, xác nhận hoặc giải quyết sau khi đã kiểm tra nguyên nhân. Chat không tự thay đổi trạng thái cảnh báo.",
                "Cảnh báo SLO",
                "/alerts"),
            "org-structure" => (
                "Vào Cơ cấu tổ chức. Đọc cây Doanh nghiệp → Nhà máy → Khu vực → Nhóm máy → Thiết bị. Thêm hoặc sửa ở đúng cấp cha; thao tác ghi yêu cầu quyền quản lý dữ liệu nền.",
                "Cơ cấu tổ chức",
                "/equipment/hierarchy"),
            "master-data" when intent.Slots.MasterDataEntity == "product" => (
                "Vào Sản phẩm / SKU → Thêm. Nhập mã, tên và đơn vị mặc định rồi Lưu. Sửa sản phẩm tại cột Thao tác; đơn vị đã có lịch sử sản xuất được hệ thống bảo vệ.",
                "Sản phẩm / SKU",
                "/master/products"),
            "master-data" when intent.Slots.MasterDataEntity == "shift" => (
                "Vào Ca làm việc → Thêm. Nhập mã, tên, nhà máy và giờ bắt đầu/kết thúc. Giờ kết thúc trước hoặc bằng giờ bắt đầu được hiểu là ca qua ngày hôm sau.",
                "Ca làm việc",
                "/master/shifts"),
            "master-data" when intent.Slots.MasterDataEntity == "user" => (
                "Vào Quản lý người dùng. Các thao tác thêm/sửa/xóa/đổi vai trò chỉ hiển thị và được thực hiện khi tài khoản có quyền quản trị tương ứng.",
                "Quản lý người dùng",
                "/users"),
            "master-data" => (
                "Vào Cơ cấu tổ chức, mở đúng cấp cha rồi chọn thêm hoặc sửa đối tượng cần quản lý. Cây hiện tại gồm Doanh nghiệp → Nhà máy → Khu vực → Nhóm máy → Thiết bị; không có danh mục công đoạn riêng.",
                "Cơ cấu tổ chức",
                "/equipment/hierarchy"),
            _ => (
                "Hãy nêu rõ thao tác: tạo kế hoạch, cấu hình mapping, thêm định mức, phân loại downtime hoặc quản lý SLO.",
                "Bảng điều khiển",
                "/")
        };

        return ChatFormatting.Answer(intent.Kind, text, title, href);
    }
}
