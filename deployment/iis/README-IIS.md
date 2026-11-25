# OEE trên IIS — hướng dẫn hiện hành

Đối chiếu source ngày 30/10/2026. Web host chứa UI/API và các hosted worker thu thập OPC, xử lý outbox, tính OEE, lập kế hoạch và PDF theo ca. Đóng tab/đăng xuất/ngắt RDP không phải lệnh dừng host; App Pool dừng, idle, recycle hoặc crash sẽ gây gián đoạn thu thập.

## Chuẩn bị

Dùng Site/App Pool riêng, x64, No Managed Code và một worker process. IIS cần ASP.NET Core Module V2 từ .NET 10 Hosting Bundle, kể cả khi Web được publish self-contained. Đặt Physical Path vào thư mục Web đã giải nén.

Chỉ một instance được phép thu thập cho cùng nguồn/database. Giữ đúng SQL, endpoint, chứng chỉ và cấu hình môi trường của máy đích. Tài khoản App Pool cần quyền đọc/chạy chương trình; cần Modify cho thư mục vận hành có ghi, gồm `App_Data` hoặc `PlanAutomation:StoragePath`, logs và kho chứng chỉ thích hợp.

## Kiểm tra và cấu hình chạy nền

Tại thư mục chứa script, mở Windows PowerShell 5.1 bằng quyền quản trị:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-IisBackground.ps1 -Port 8010
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Configure-Iis.ps1 -Port 8010
```

Nếu port không xác định duy nhất Site, dùng `-SiteName 'Tên Site OEE'`. Đọc kết quả trước khi áp dụng: Configure-Iis thay đổi cấu hình và khởi động/recycle App Pool. Script không tạo/cập nhật database hoặc thay chuỗi kết nối. Nó đặt AlwaysRunning, idle timeout 0, preload và không recycle chồng hai process; vẫn cần kiểm tra quyền ghi App_Data/storage ngoài theo triển khai thực tế.

## Cập nhật

1. Sao lưu thư mục Web và cấu hình riêng để có thể quay lại bản trước.
2. Dừng đúng Site/App Pool OEE để giải phóng DLL. Không dừng Site khác.
3. Chép nội dung Web của gói cập nhật; giữ appsettings, cấu hình môi trường, web.config riêng, App_Data/storage ngoài và chứng chỉ. Đối chiếu manifest của đúng gói. Không dùng file example đè cấu hình máy thật.
4. Xác nhận cấu hình hiệu lực có kết nối SQL đúng, BackgroundProcessing.Enabled và OpcUa.Enabled phù hợp. Environment variables, user secrets ở Development hoặc OEE_SETTINGS_FILE có thể ghi đè.
5. Khởi động; kiểm tra đăng nhập, CSS, kế hoạch đang chạy, định mức, tag và sản lượng ca. HTTP 200 không chứng minh counter/checkpoint/output hoạt động đúng.

## Kiểm chứng không có trình duyệt

Ghi timestamp nhận OPC, checkpoint, output và OEE; đóng tab, ngắt RDP hơn 25 phút. Kết nối lại và chạy Test-IisBackground trước khi mở Web. Timestamp SQL probe dùng UTC; cấu hình mà probe đọc có thể khác cấu hình hiệu lực của host. OEE snapshot chỉ đổi với kỳ/output được worker chọn; counter không đổi có thể vẫn là dữ liệu hợp lệ. Muốn xác nhận thu thập, kiểm tra cả thời điểm nhận và giá trị/tag đúng nguồn.

Các sổ tay có thể chỉnh sửa trong repository: `output/word/So_tay_van_hanh_OEE.docx` và `output/word/So_tay_ky_su_OEE.docx`; bản PDF cùng tên ở `output/pdf/`. Hướng dẫn trong gói đã bàn giao thuộc riêng phiên bản đó; không sửa gói cũ đã có checksum.
